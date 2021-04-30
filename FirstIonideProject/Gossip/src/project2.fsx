// #time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 

open System
open Akka.Actor
open Akka.FSharp

let system = ActorSystem.Create("FSharp")
let rnd = System.Random()
let gossipThreshold = 10
let pushSumThreshold = 3
let mutable numNodes:int = 0
let mutable rowSz:int = 0 
let mutable topology, algorithm = "",""

// For the purpose of creating different topologies, we decided to use a flat list of actor references.
// Based on the type of the topology in consideration, the random neighbor selection logic varies.
// For instance, for a 2D topology, If we divide the current index by the row size of the grid, we get the 
// corresponding row number of an element in the workersList. Similarly, the column number is determined by 
// taking modulus of the current element's index with row size.
let mutable workersList = [||]

let mutable actorStates= [||]   
let mutable actorStatesPushSum = [||]
let pushSumPrecision: float = float(10) ** float(-10)

type BossMessage = 
    | BossMessage of int
    | WorkerTaskFinished of int

type WorkerMessage = 
    | WorkerMessage of int * string
    | WorkerMessagePushSum of int * float * float

let findIndex arr elem = 
    try
        arr |> Array.findIndex ((=) elem)
    with
    | _ -> -1

let getRandomNeighborFull (idx:int): int =
    // get random index from the full network
    let mutable randNbr = rnd.Next()% numNodes
    while randNbr = idx do
        randNbr <- rnd.Next()% numNodes
    randNbr

let getRandomNeighbor2D (idx: int) (isImperfect: bool): int =
    (* 
        If we divide the current index by the row size of the grid, we get the 
        corresponding row number of an element in the workersList. Similarly, the column number is determined by 
        taking modulus of the current element's index with row size.

        In case the isImperfect flag is True, we also consider an extra random cell from the grid
    *)
    let mutable neighs = [||]
    let mutable randNbr = -1
    let r = int idx / rowSz
    let c = idx % rowSz

    if (r+1)< rowSz then
        neighs <- Array.append neighs [|((r+1)*rowSz)+c|]
    if r-1>= 0 then
        neighs <- Array.append neighs [|((r-1)*rowSz)+c|]
    if c+1< rowSz then
        neighs <- Array.append neighs [|(r*rowSz)+c+1|]
    if c-1>= 0 then
        neighs <- Array.append neighs [|(r*rowSz)+c-1|]
    
    // if imperfect, then add one more random neighbor from the grid which is not equal
    // to either the node or its immediate neighbors
    if isImperfect then
        randNbr <- getRandomNeighborFull idx
        while (findIndex neighs randNbr) > -1 do
            randNbr <- getRandomNeighborFull idx
        neighs <- Array.append neighs [|randNbr|]
    neighs.[rnd.Next() % neighs.Length]
    

let getRandomNeighborLine (idx: int): int =
    
    let mutable randNbr = -1
    // If the first element in the list, then consider only right neighbor
    if idx = 0 then
        randNbr <- 1
    // If the last element in the list, consider the element before as neighbor
    elif idx = numNodes-1 then
        randNbr <- numNodes-2
    else
        randNbr <- idx - 1
        let randState = rnd.Next()%2
        if randState = 1 then
            randNbr <- idx + 1
    randNbr

let getRandomNeighbor (idx:int): int =
    let mutable randNbr = idx
    match topology.ToLower() with
        | "full" -> 
            let a = getRandomNeighborFull idx
            randNbr <- a
        | "2d" -> 
            let a =  getRandomNeighbor2D idx false
            randNbr <- a
        | "line" -> 
            let a = getRandomNeighborLine idx
            randNbr <- a
        | "imp2d" -> 
            let a = getRandomNeighbor2D idx true
            randNbr <- a
        | _ ->
            printfn "Invalid topology given."
    // chose one on neighs and send its idx in the arr
    // printfn "for idx: %d | topo : %s | random selected neighbor is: %d | neighbours are %A" idx topology randNbr neighs
    randNbr

// *********** WORKER ACTOR LOGIC **********
let GossipActor (mailbox: Actor<_>) =
    (*
        || GOSSIP algorithm ||
            For convergance in Gossip algorithm:
                Expected termination condition -> Stop at a heard count of 10 for any node. Does not guarantee complete distribution of gossip.
                Implemented termination condition -> Stop when the global min heard count reaches 10. Guarantees complete distribution of gossip.
        
        || PUSH-SUM algorithm ||
            For convergance in Push-sum algorithm:
                Expected termination condition -> Stop when a node's ratio changes by an amount less than 10^-10 for 3 consecutive times. 
                                                    Does not guarantee complete distribution of gossip.
                
                Implemented termination condition -> Stop when the global count for all the nodes is three for the ratio condition. 
                                                        Guarantees complete distribution of sum.
    *)
    let mutable hcount=0
    let mutable s: float = -1.0
    let mutable w: float = 1.0
    let mutable diff: float = 0.0
    let hasConverged: bool = false

    let rec loop() = actor {
        let! msg = mailbox.Receive()
        match msg with
        // gossip logic
        | WorkerMessage(idx , gossip) ->
            hcount <- hcount+1
            actorStates.[idx] <- hcount

            (* ****************Hardstop as per the requirements****************
                if hcount < threshold then
            ****************Forced hasConverged**************)

            if (actorStates |> Array.min) < gossipThreshold then
               let randNbr = getRandomNeighbor idx
               workersList.[randNbr] <! WorkerMessage(randNbr, "gossip")
            else
                select "/user/SupervisorActor" system <! WorkerTaskFinished(1)

        // Push sum logic
        | WorkerMessagePushSum(idx, sIn, wIn) ->
            if (actorStates |> Array.min) < pushSumThreshold then
                // initial setup
                if s = -1.0 then
                    s <- float idx

                // update current sum, weight and ratio
                s <- s + sIn
                w <- w + wIn
                let currentRatio: float = float (s) / float (w)
                diff <- abs(actorStatesPushSum.[idx] - currentRatio)
                actorStatesPushSum.[idx] <- currentRatio
                
                // if ratio does not change by at least pushSumPrecision, increase heard count
                if diff < pushSumPrecision then
                    hcount <- hcount + 1
                else
                // resetting the count as the consecutive streak is broken now
                    hcount <- 0

                // update global heard count
                actorStates.[idx] <- hcount

                // sending the message to a neighbour
                let randNbr = getRandomNeighbor idx
                workersList.[randNbr] <! WorkerMessagePushSum(randNbr, s / float(2), w / float(2))
            else
                // checking the termination condition here
                printf "Push-sum converged with %f\n"  (float (s) / float (w))
                select "/user/SupervisorActor" system <! WorkerTaskFinished(1)

            
        return! loop()
    }
    loop()


// *************** SUPERVISOR ACTOR'S HELPER UTILITY **************
let supervisorHelper (start:int)= 
    // spawn all the actors, initialize states
    workersList <- [| for i in 1 .. numNodes -> spawn system (string i) GossipActor |]
    actorStates <-  Array.zeroCreate numNodes
    actorStatesPushSum <-  Array.zeroCreate numNodes
    
    printfn "# of Nodes = %d\n" numNodes

    match algorithm with
    | "gossip" ->
        workersList.[start] <! WorkerMessage(start,"gossip")
    | "push-sum" ->
        workersList.[start] <! WorkerMessagePushSum(start,0.0,0.0)
    | _ ->
        printfn "Error: Invalid topology" 
    

// *********** SUPERVISOR ACTOR LOGIC **********
let SupervisorActor (mailbox: Actor<_>) = 
    // start the timer
    let stopWatch = System.Diagnostics.Stopwatch.StartNew()

    let rec loop () = actor {
        let! msg = mailbox.Receive ()
        match msg with
        // Process main input
        | BossMessage(start) ->
            supervisorHelper start
        // Algorithm terminated. Print the elapsed time and other outputs
        | WorkerTaskFinished(c) -> 
            stopWatch.Stop()
            printfn "Total run time = %fms" stopWatch.Elapsed.TotalMilliseconds
            // printfn "%A" actorStates
            // if algorithm = "push-sum" then
            //     printfn "%A" actorStatesPushSum
            
        return! loop ()
    }
    loop ()


let main(args: array<string>) = 
    let N,topo,algo = int(args.[3]),string(args.[4]),string(args.[5])
    let mutable errorFlag = false
    numNodes <-N
    topology<-topo
    algorithm<-algo.ToLower()
    let actorRef  = spawn system "SupervisorActor" SupervisorActor
    match algo.ToLower() with
        | "gossip" ->
            printfn "gossip"
        | "push-sum" ->
            printfn "push-sum"
        | _ ->
            errorFlag <- true
            printfn "ERROR: Algorithm not present"

    match topology.ToLower() with
        | "full" -> 
            printfn "full"
        | "2d" -> 
            printfn "2D" 
            rowSz <- numNodes |> float |> sqrt |> ceil |> int 
            numNodes <- (rowSz * rowSz)
            printfn "# of Nodes rounded up to:%d" numNodes
        | "line" -> 
            printfn "line"
        | "imp2d" -> 
            rowSz <- numNodes |> float |> sqrt |> ceil |> int 
            numNodes <- (rowSz * rowSz)
            printfn "# of Nodes rounded up to:%d" numNodes
            printfn "imp2D"
        | _ -> 
            errorFlag <- true
            printfn "ERROR: Topology '%s' not implemented." topology
            printfn "Valid topologies are: full, 2D, imp2D, line."

    if not errorFlag then
        actorRef <! BossMessage 0

main(Environment.GetCommandLineArgs())
// If not for the below line, the program exits without printing anything.
// Please press any key once the execution is done and CPU times have been printed.
// You might have to scroll and see the printed CPU times. (Because of async processing)
System.Console.ReadKey() |> ignore
