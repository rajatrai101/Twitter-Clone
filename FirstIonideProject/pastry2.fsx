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

let display msg = 
    printer <! ShowStr(msg)

let hex2int code = Convert.ToInt32(code, l)

// returns a hexadecimal representation string of a number of length numDigits by padding leading zeros if necessary
let int2hex (num:int) = 
    let mutable hexStr = num.ToString("X")
    if hexStr.Length < numDigits then
        hexStr <- String.concat  "" [String.replicate (numDigits-hexStr.Length) "0"; hexStr]
    hexStr

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
                let mutable missCount = 0
                let number = Convert.ToInt32(id, l)
                for i in [1..l/2] do
                    if number-i<0 then
                        missCount <- missCount + 1
                        s <- int2hex(number+(l/2)+missCount)
                    else
                        s <- int2hex(number-i)
                    leafSet <- leafSet.Add((s))
                    if number+i>=numNodes then
                        missCount <- missCount + 1
                        s <- int2hex(number-(l/2)-missCount)
                    else
                        s <- int2hex(number+i)
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
                display (sprintf "Source: %s -> Destination: %s\nAt : %s\n" source key id)
                if key = id then
                    printer <! ShowStr(sprintf "--------------REACHED-----------!!!\n #ofHops: %A\n" hops)
                    if actorHopsMap.ContainsKey source then
                        let total, avgHops = actorHopsMap.[source].[1], actorHopsMap.[source].[0]
                        actorHopsMap.[source].[0] <- ((avgHops*total)+hops)/(total+1.0)
                        actorHopsMap.[source].[1] <- total + 1.0
                    else
                        let mutable tempArr = [|hops;1.0|]
                        actorHopsMap <- actorHopsMap.Add (source, tempArr)

                elif leafSet.Contains key then
                    display "The key is found inside the leafset..."
                    actorMap.[key] <! Route(key, source, hops + 1.0)

                elif (hex2int(key) >= hex2int(leafSet.MinimumElement)) && (hex2int(key) <= hex2int(leafSet.MaximumElement)) then
                    display "The key is in the range of the leaf set..."
                    // If the destination key is in the range of the leafset, then
                    //  forward it to the nearest possible node in the leafset
                    
                    let mutable dist = 0
                    let mutable distMin = 2147483647
                    let mutable nextNodeId = ""
                    for candidateNode in leafSet do
                        dist <- abs (hex2int(candidateNode) -  hex2int(key))
                        if dist < distMin then
                            distMin <- dist
                            nextNodeId <- candidateNode
                    display (sprintf "Forwarding the message to %s..." nextNodeId)
                    actorMap.[nextNodeId] <! Route(key, source, hops + 1.0)
                else
                    // display (sprintf "Routing table: %A\n" routingTable)
                    display "The key was not in the leaf set range. Finding next hop in the routing table..."
                    let mutable i = 0
                    while key.[i] = id.[i] do
                        i <- i + 1
                    let sharedPrefixLength = i
                    let check = 0
                    let rtrow = sharedPrefixLength
                    // display (sprintf "Shared Prefix Len : %d" sharedPrefixLength)
                    let mutable rtcol = Convert.ToInt32(string(key.[sharedPrefixLength]), l)
                    if not (isNull routingTable.[rtrow, rtcol]) then
                        display (sprintf "Next hop found in the routing table. Forwarding to %s" routingTable.[rtrow,rtcol])
                        actorMap.[routingTable.[rtrow,rtcol]] <! Route(key, source, hops+1.0)
                    else
                        if hops >= float(numDigits) then
                            actorMap.[key] <! Route(key, source, hops)
                        else
                            display "Routing table entry is null. Entered the rare case..."
                            display "Determining the next node from either the leafset or routing table or neighborhood..."
                            let mutable dist = 0
                            let mutable distMin = 2147483647
                            let mutable nextNodeId: string = ""
                            let mutable shl = 0

                            // Distance between the current node and the destination node
                            let distCurrToDest = abs(hex2int(id) - hex2int(key))
                            
                            for candidateNode in leafSet do
                                while candidateNode.[shl] = id.[shl] do 
                                    shl <- shl + 1
                                if shl >= sharedPrefixLength then
                                    dist <- abs (hex2int(candidateNode) -  hex2int(id))
                                    if dist < distCurrToDest then
                                        nextNodeId <- candidateNode
                                shl <- 0

                            // Now check in the routing table only if no next node was determined in the leaf set
                            if nextNodeId = "" then
                                let mutable r = 0
                                let mutable c = 0
                                let mutable breakTheLoop = false

                                while not breakTheLoop && (r < numDigits) do
                                    while not breakTheLoop && (c < l) do
                                        if not (isNull routingTable.[r,c]) then
                                            let mutable candidateNode = routingTable.[r,c]
                                            // get the shared prefix length
                                            while candidateNode.[shl] = id.[shl] do
                                                shl <- shl + 1
                                            // Proceed only if the shared prefix length is as long as with the current node
                                            if shl >= sharedPrefixLength then
                                                // Distance between the candidate node and the destination.
                                                // The candidate key can either be from the leafset of the routing table.
                                                dist <- abs(hex2int(candidateNode) - hex2int(key))
                                                // If the distance is less than the distance between current node and the destination,
                                                // then forward the message to this candidate node
                                                if dist < distCurrToDest then
                                                    nextNodeId <- candidateNode
                                            shl <- 0
                                        c <- c + 1
                                    r <- r + 1
                            display (sprintf "Forwarding to the next node: %s" nextNodeId)
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
    let mutable refNode:string= String.replicate numDigits "0"
    let mutable hexNum:string=""
    let mutable len = 0
    let mutable reflen = 0
    printf "Node Id: %s\n" nodeId
    let mutable actor = spawn system nodeId NodeActor
    actor <! Init nodeId
    actorMap <- actorMap.Add(nodeId,actor)
    display "\n\nBuilding the network...\n\n"
    let mark25pct = int(float(numNodes)*0.25)
    let mark50pct = int(float(numNodes)*0.50)
    let mark75pct = int(float(numNodes)*0.75)
    for i in 1 .. numNodes-1 do
        
        if i = mark25pct then
            display "25% of network constructed"
        if i = mark50pct then
            display "50% of network constructed"
        if i = mark75pct then
            display "75% of network constructed"

        hexNum <- int2hex(i)
        len <- hexNum.Length
        nodeId <- hexNum
        actor <- spawn system (string nodeId) NodeActor
        actor <! Init(nodeId)
        if i > numNodes/2 then
            refNode <- int2hex(rnd.Next()%(numNodes/2))
        elif i> numNodes/4 then
            refNode <- int2hex(rnd.Next()%(i))
        reflen <- refNode.Length
        // printf "%s numDigits-len %d\n" hexNum (numDigits-len)
        refNode <- String.concat  "" [String.replicate (numDigits-reflen) "0"; refNode]
        actorMap <- actorMap.Add(nodeId,actor)
        actorMap.[refNode] <! Join(nodeId,0)
        System.Threading.Thread.Sleep(5)

    if false then
        for i in [0..numNodes-1] do
            hexNum <- int2hex(i)
            len <- hexNum.Length
            nodeId <- String.concat  "" [String.replicate (numDigits-len) "0"; hexNum]
            actorMap.[nodeId] <! ShowTable

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
        printf "Rand src:%s dst:%s\n" src dst
        actorMap.[src] <! Route(dst,src,0.0)
        System.Threading.Thread.Sleep(1000)

    display "Requests processed"
    let mutable totalHopSize:double = 0.0
    display "Computing average hop size"
    System.Threading.Thread.Sleep(3000)

    for pair in actorHopsMap do
        totalHopSize <- totalHopSize + pair.Value.[0]

    display (sprintf "Average Hop Size: %f" (totalHopSize / float(actorHopsMap.Count)))

main(Environment.GetCommandLineArgs())
// If not for the below line, the program exits without printing anything.
// Please press any key once the execution is done and CPU times have been printed.
// You might have to scroll and see the printed CPU times. (Because of async processing)
System.Console.ReadKey() |> ignore