#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.FSharp

// ASSUMPTIONS
let b: int = 4
let l: int = 2.0**float(b) |> int

let system = ActorSystem.Create("FSharp")
let rnd = System.Random()
let mutable numRequests:int = 0
let mutable numNodes:int = 0
let mutable numDigits:int = 0
let mutable actorMap = Map.empty
let mutable actorHopsMap: Map<string, double[]> = Map.empty
let mutable srcdst:Map<String,String> = Map.empty

type BossMessage = 
    | BossMessage of int
    | WorkerTaskFinished of int

type WorkerMessage = 
    | Init of string
    | Join of string * int
    | Route of string * string * double
    | UpdateRoutingTable of string[]
    | ShowTable
    | ShowLeaf of string * Set<string>
    | ShowStr of string

let PrinterActor (mailbox: Actor<_>) =
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        match msg with
        | ShowLeaf(nodeId, leafSet) ->
            printf "For node %s len:%d = " nodeId leafSet.Count
            for e in leafSet do
                printf "%s " e
            printfn ""
        | ShowStr(str) ->
            printfn "%s" str
        | _ ->
            printf ""
        return! loop()
    }
    loop()

let mutable printer = spawn system "PrinterActor" PrinterActor

let NodeActor (mailbox: Actor<_>) = 
    // count keeps track of all the workers that finish their work and ping back to the supervisor
    // *****************************************
    let mutable id: string = ""
    let cols = l
    let mutable leafSet: Set<string> = Set.empty
    let mutable neighborSet: Set<string> = Set.empty
    let mutable routingTable: string[,] = Array2D.zeroCreate numDigits l
    let mutable currentRow = 0
    let mutable s = null
    let rec loop () = actor {
        let! msg = mailbox.Receive ()
        match msg with
            // Initialization phase of a network node
            | Init(passedId) ->
                id <- passedId
                let number = Convert.ToInt32(id, l)
                for i in [1..l/2] do
                    if number-i<0 then
                        s <- (numNodes-i).ToString("X")
                    else
                        s <- (number-i).ToString("X")
                    s <- String.concat  "" [String.replicate (numDigits-s.Length) "0"; s]
                    leafSet <- leafSet.Add((s))
                    if number+i>=numNodes then 
                        s <- (number-i-numNodes).ToString("X")
                    else
                        s <- (number+i).ToString("X")
                    if numDigits>s.Length then
                        s <- String.concat  "" [String.replicate (numDigits-s.Length) "0"; s]
                    leafSet <- leafSet.Add((s))

            // Updates routing table for a new node
            | Join (nodeId, currentIndex) ->
                let mutable i = 0
                let mutable k = currentIndex
                // keep incrementing counter while same characters are encountered in the hex node IDs
                while nodeId.[i] = id.[i] do
                    i <- i + 1
                let sharedPrefixLength = i
                let mutable routingRow = Array.zeroCreate l
                while k<=sharedPrefixLength do
                    let mutable routingRow: string[] = Array.init l (fun x -> routingTable.[k,x])
                    routingRow.[Convert.ToInt32(string(id.[sharedPrefixLength]),l)] <- id
                    actorMap.[nodeId] <! UpdateRoutingTable(routingRow)
                    k <- k + 1
                let rtrow = sharedPrefixLength
                let rtcol = Convert.ToInt32(string(nodeId.[sharedPrefixLength]),l)
                if isNull routingTable.[rtrow,rtcol] then
                    routingTable.[rtrow,rtcol] <- nodeId
                else
                    actorMap.[routingTable.[rtrow,rtcol]] <! Join(nodeId,k)
            // Update the current row of the routing table with the given row
            | UpdateRoutingTable(routingRow) ->
                routingTable.[currentRow,*] <- routingRow
                currentRow <- currentRow + 1
            // Routes a message with destination as key from source where hops is the hops traced so far
            | Route(key, source, hops) ->
                printer <! ShowStr(sprintf "At : %s\n%s -> %s\n" id source key)
                // printer <! ShowStr(sprintf "Routing Table %A\n" routingTable)
                // printer <! ShowStr(sprintf "Leaf Set %A" leafSet)
                // printer <! ShowStr(sprintf "--------------------------------")
                if key = id then
                    printer <! ShowStr(sprintf "--------------REACHED-----------!!!%s->%s\n #ofHops: %A\n" source key hops)
                    if actorHopsMap.ContainsKey source then
                        let total, avgHops = actorHopsMap.[source].[1], actorHopsMap.[source].[0]
                        actorHopsMap.[source].[0] <- ((avgHops*total)+hops)/(total+1.0)
                        actorHopsMap.[source].[1] <- total + 1.0
                    else
                        // printf "actorsHop dsnt have the key\n"
                        let mutable tempArr = [|hops;1.0|]
                        actorHopsMap <- actorHopsMap.Add (source, tempArr)
                        // printer <! ShowStr(sprintf "actors Hop map No ERR")
                elif leafSet.Contains key then
                    // printer <! ShowStr(sprintf "Second Case!!\n")
                    actorMap.[key] <! Route(key, source, hops + 1.0)
                else
                    // printer <! ShowStr(sprintf "Third Case!!")
                    let mutable i = 0
                    while key.[i] = id.[i] do
                        i <- i + 1
                    let sharedPrefixLength = i
                    let check = 0
                    let rtrow = sharedPrefixLength
                    // printer <! ShowStr(sprintf "NumberOfDigit: %d\nShared Prefix Len : %d" numDigits sharedPrefixLength)
                    let mutable rtcol = Convert.ToInt32(string(key.[sharedPrefixLength]), l)
                    // printer <! ShowStr(sprintf "row: %d | col: %d | routingTable.[rtrow,rtcol]: %s" rtrow rtcol routingTable.[rtrow,rtcol])
                    if not (isNull routingTable.[rtrow, rtcol]) then
                        // printer <! ShowStr(sprintf "----------------NEXT----------------")
                        actorMap.[routingTable.[rtrow,rtcol]] <! Route(key, source, hops+1.0)
                    else
                        // printer <! ShowStr(sprintf "Finding Min Dist node\n")
                        let mutable dist = 0
                        let mutable distMin = 2147483647
                        let mutable nextNodeId = ""
                        let mutable shl = 0

                        for candidateNode in leafSet do
                            // printer <! ShowStr(sprintf "sab thik 1")
                            while candidateNode.[shl] = id.[shl] do 
                                shl <- shl + 1
                            // printer <! ShowStr(sprintf "sab thik 2")
                            if shl >= sharedPrefixLength then
                                dist <- abs (Convert.ToInt32(candidateNode, l) -  Convert.ToInt32(id, l))
                                if dist < distMin then
                                    nextNodeId <- candidateNode
                            shl <- 0
                        // printer <! ShowStr(sprintf "nextNodeId: %s" nextNodeId)
                        // printer <! ShowStr(sprintf "----------------NEXT----------------")
                        actorMap.[nextNodeId] <! Route(key, source, hops + 1.0)
                

            | ShowTable ->
                printer <! ShowLeaf(id,leafSet)
                // printfn "Agaya"
                // for e in leafSet do
                //     printf "%s " e
                // printfn "__________________"
                // for i = 0 to numDigits-1 do
                //     // for j = 0 to l-1 do
                //     //     printf "%s " routingTable.[i,j]
                //     printfn "%A" routingTable.[i,*]
                // printfn "============================================================="
            |_ ->
                printfn "Error!\ns"
            
        return! loop ()
    }
    loop ()

let Keys(map: Map<'K,'V>) =
    seq {
        for KeyValue(key,value) in map do
            yield key
    } |> List.ofSeq

let main(args: array<string>) = 
    let n,r = int(args.[3]),int(args.[4])
    let mutable errorFlag = false
    numNodes <-n
    numRequests <-r
    numDigits <- Math.Log(numNodes|> double, 16.) |> ceil |> int
    printf "N:%d\nR:%d\nNumber of digits:%d\n" numNodes numRequests numDigits
    printf "Network construction initiated"
    let mutable nodeId:string=String.replicate numDigits "0"
    let mutable hexNum:string=""
    let mutable len = 0
    printf "Node Id: %s\n" nodeId
    let mutable actor = spawn system nodeId NodeActor
    actor <! Init nodeId
    actorMap <- actorMap.Add(nodeId,actor)
    for i in 1 .. numNodes-1 do
        // if i = numNodes/4 then
        //     printf "25% of network constructed"
        // elif i= numNodes/2 then
        //     printf "50% of network constructed"
        // elif i=numNodes*3/4 then
        //     printf "75% of network constructed"
        hexNum <- i.ToString("X")
        len <- hexNum.Length
        nodeId <- String.concat  "" [String.replicate (numDigits-len) "0"; hexNum]
        printf "\nNode creating %s\n" nodeId
        actor <- spawn system (string nodeId) NodeActor
        actor <! Init(nodeId)
        actorMap <- actorMap.Add(nodeId,actor)
        actorMap.[String.replicate numDigits "0"] <! Join(nodeId,0)
        System.Threading.Thread.Sleep(100)
    
    if false then
        for i in [0..numNodes-1] do
            hexNum <- i.ToString("X")
            len <- hexNum.Length
            nodeId <- String.concat  "" [String.replicate (numDigits-len) "0"; hexNum]
            actorMap.[nodeId] <! ShowTable
            System.Threading.Thread.Sleep(100)

    printf "\nNetwork is built!!!\n"
    let actorsArray = Keys(actorMap)

    // printf  "ActorMap Keys :%A\nlength : %d\n" actorsArray actorsArray.Length
    // printf  "ActorMap Keys :%s \n" actorsArray.[numNodes-1]
    let mutable src,dst= null, null

    printfn "Waiting for 5 seconds....\n\n\n\n"
    System.Threading.Thread.Sleep(5000)

    for i in 0..numRequests-1 do
        src <- actorsArray.[i%actorsArray.Length] 
        dst <- actorsArray.[rnd.Next()%actorsArray.Length]
        while dst = src do
            dst <- actorsArray.[rnd.Next()%actorsArray.Length]
        // printer <! ShowStr(sprintf "Rand src:%s dst:%s" src dst)
        actorMap.[src] <! Route(dst,src,0.0)
        System.Threading.Thread.Sleep(2000)

    
main(Environment.GetCommandLineArgs())
// If not for the below line, the program exits without printing anything.
// Please press any key once the execution is done and CPU times have been printed.
// You might have to scroll and see the printed CPU times. (Because of async processing)
System.Console.ReadKey() |> ignore
