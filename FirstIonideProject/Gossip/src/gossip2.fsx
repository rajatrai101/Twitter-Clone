// #time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 

open System
open Akka.Actor
open Akka.FSharp

let system = ActorSystem.Create("FSharp")
let rnd = System.Random()

let mutable numNodes:int = 0
let mutable rowSz:int = 0 
let mutable topology, algorithm = "",""
let mutable workersList = [||]
let mutable actorStates= [||]   
type BossMessage = 
    | BossMessage of int
    | WorkerTaskFinished of int

type WorkerMessage = WorkerMessage of int * string

let findIndex arr elem = 
    try
        arr |> Array.findIndex ((=) elem)
    with
    | _ -> -1

let getRandomNeighborFull (idx:int) = 
    let mutable randNbr = -1
    let mutable randState = rnd.Next()% numNodes
    let mutable neighs = [||]
    while randState = idx do
        randState <- rnd.Next()% numNodes
    neighs <- [|randState|]
    randNbr <- randState
    randNbr, neighs

let getRandomNeighbor2D (idx: int) (isImperfect: bool) =
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
        randNbr <- fst (getRandomNeighborFull idx)
        while (findIndex neighs randNbr) > -1 do
            randNbr <- fst (getRandomNeighborFull idx)
        neighs <- Array.append neighs [|randNbr|]   
    randNbr <- neighs.[rnd.Next() % neighs.Length]
    randNbr, neighs

let getRandomNeighborLine (idx: int): int * int[] =
    let mutable neighs = [||]
    let mutable randNbr = -1
    if numNodes > 1 then
        if idx = 0 then
            randNbr <- 1
        elif idx = numNodes-1 then
            randNbr <- numNodes-2
        else
            randNbr <- idx - 1
            let randState = rnd.Next()%2
            if randState = 1 then
                randNbr <- idx + 1
    neighs <- Array.append neighs [|randNbr|]
    randNbr, neighs

let getRandomNeighbor (idx:int) =
    let mutable neighs = [||]
    let mutable randNbr = idx
    match topology with
        | "full" -> 
            // let mutable randState = rnd.Next()% numNodes
            // while randState = idx do
            //     randState <- rnd.Next()% numNodes
            // neighs <- [|randState|]
            // randNbr <- randState
            let a, b = getRandomNeighborFull idx
            randNbr <- a
            neighs <- b
        | "2D" -> 
            // let r = int idx / rowSz
            // let c = idx % rowSz
            // if (r+1)< rowSz then
            //     neighs <- Array.append neighs [|((r+1)*rowSz)+c|]
            // if r-1>= 0 then
            //     neighs <- Array.append neighs [|((r-1)*rowSz)+c|]
            // if c+1< rowSz then
            //     neighs <- Array.append neighs [|(r*rowSz)+c+1|]
            // if c-1>= 0 then
            //     neighs <- Array.append neighs [|(r*rowSz)+c-1|]
            // randNbr <- rnd.Next() % neighs.Length
            let a, b =  getRandomNeighbor2D idx false
            randNbr <- a
            neighs <- b

        | "line" -> 
            // if numNodes > 1 then
            //     if idx = 0 then
            //         randNbr <- 1
            //     elif idx = numNodes-1 then
            //         randNbr <- numNodes-2
            //     else
            //         randNbr <- idx - 1
            //         let randState = rnd.Next()%2
            //         if randState = 1 then
            //             randNbr <- idx + 1
            let a, b = getRandomNeighborLine idx
            randNbr <- a
            neighs <- b
        | "imp2D" -> 
            let a, b = getRandomNeighbor2D idx true
            randNbr <- a
            neighs <- b
    // chose one on neighs and send its idx in the arr
    printfn "for idx: %d | topo : %s | random selected neighbor is: %d | neighbours are %A" idx topology randNbr neighs
    randNbr

// *********** WORKER ACTOR LOGIC **********
let GossipActor (mailbox: Actor<_>) =
    let mutable hcount=0
    let rec loop() = actor {
        let! WorkerMessage(idx , gossip) = mailbox.Receive()
        hcount <- hcount+1
        printf "idx: %d heardCount %d minheard %d\n" idx hcount (actorStates |> Array.min)
        actorStates.[idx] <- hcount
        printfn ""
        if (actorStates |> Array.min) = 3 then
            mailbox.Sender() <! WorkerTaskFinished(1)
            printf "Done  Msg %s\n" gossip
            printfn "%A" actorStates
        else
            let randNbr = getRandomNeighbor idx
            workersList.[randNbr] <! WorkerMessage(randNbr, "gossip")

        return! loop()
    }
    loop()


// *************** SUPERVISOR ACTOR'S HELPER UTILITY **************
let supervisorHelper (start:int)= 
    workersList <- [| for i in 1 .. numNodes -> spawn system (string i) GossipActor |]
    // for push-sum  add cond
    // if algo = 'gossip then 
    actorStates <-  Array.zeroCreate numNodes
    //else
    // actorStates   [[si,wi].... numNodes]
    printfn "Capacity: %i" workersList.Length
    printfn "# of Nodes = %d\nTopology = %s\nAlgorithm = %s" numNodes topology algorithm
    workersList.[start] <! WorkerMessage(start,"gossip")
    

// *********** SUPERVISOR ACTOR LOGIC **********
let SupervisorActor (mailbox: Actor<_>) = 
    // count keeps track of all the workers that finish their work and ping back to the supervisor
    // *****************************************
    let stopWatch = System.Diagnostics.Stopwatch.StartNew()

    let rec loop () = actor {
        let! msg = mailbox.Receive ()
        match msg with
        // Process main input
        | BossMessage(start) ->
            supervisorHelper start
        | WorkerTaskFinished(c) -> 
            printfn "%A" actorStates
            printfn "========\nResults:"
            printfn "================\n"
            stopWatch.Stop()
            printfn "Total run time = %fms" stopWatch.Elapsed.TotalMilliseconds
        return! loop ()
    }
    loop ()


let main(args: array<string>) = 
    let N,topo,algo = int(args.[3]),string(args.[4]),string(args.[5])
    let mutable errorFlag = false
    numNodes <-N
    topology<-topo
    algorithm<-algo
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
        | "2D" -> 
            printfn "2D" 
            rowSz <- numNodes |> float |> sqrt |> ceil |> int 
            numNodes <- (rowSz * rowSz)
            printfn "# of Nodes rounded up to:%d" numNodes
        | "line" -> 
            printfn "line"
        | "imp2D" -> 
            rowSz <- numNodes |> float |> sqrt |> ceil |> int 
            numNodes <- (rowSz * rowSz)
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
system.Terminate()
