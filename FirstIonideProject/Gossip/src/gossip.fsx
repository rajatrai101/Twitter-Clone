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

let getRandomNeighbor (idx:int) =
    let mutable neighs = [||]
    match topology with
        | "full" -> 
            let mutable randState = rnd.Next()% numNodes
            while randState = idx do
                randState <- rnd.Next()% numNodes
            neighs <- [|randState|]
        | "2D" -> 
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
        | "line" -> 
            let mutable randState = rnd.Next()% 2
            if numNodes >1 then
                if idx = 0 then
                    neighs <- Array.append neighs [|1|]
                elif idx = numNodes-1 then
                    neighs <- Array.append neighs [|numNodes-2|]
                elif randState = 0 then
                    neighs <- Array.append neighs [|idx-1|]
                else
                    neighs <- Array.append neighs [|idx+1|]
        | "imp2D" -> 
            printfn "imp2D"
        |_ ->
            printfn "UnDEf!"
    neighs.[(rnd.Next()%neighs.Length)]


// *********** WORKER ACTOR LOGIC **********
let GossipActor (mailbox: Actor<_>) =
    let mutable hcount=0
    let rec loop() = actor {
        let! WorkerMessage(idx , gossip) = mailbox.Receive()
        hcount <- hcount+1
        actorStates.[idx] <- hcount
        // printf "idx: %d heardCount %d minheard %d\n" idx hcount (actorStates |> Array.min)
        if (actorStates |> Array.min) = 10 then //END Cond
            select "/user/SupervisorActor" system <! WorkerTaskFinished(1)
        else
            let nextRandNeigh = getRandomNeighbor idx
            // printf "Next Random nieghbours %d\n" nextRandNeigh
            workersList.[nextRandNeigh] <! WorkerMessage(nextRandNeigh,"gossip")

        return! loop()
    }
    loop()


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
            workersList <- [| for i in 1 .. numNodes -> spawn system (string i) GossipActor |]
            actorStates <-  Array.zeroCreate numNodes
            printfn "# of Nodes = %d\nTopology = %s\nAlgorithm = %s" numNodes topology algorithm
            workersList.[start] <! WorkerMessage(start,"gossip")
    
        | WorkerTaskFinished(c) -> 
            printfn "%A" actorStates
            stopWatch.Stop()
            printfn "Total run time = %fms" stopWatch.Elapsed.TotalMilliseconds
        return! loop ()
    }
    loop ()


let main(args: array<string>) = 
    let n,topo,algo = int(args.[3]),string(args.[4]),string(args.[5])
    let mutable errorFlag = false
    numNodes <-n
    topology<-topo
    algorithm<-algo
    let actorRef  = spawn system "SupervisorActor" SupervisorActor
    // match algo with
    //     | "gossip" ->
    //         printfn "gossip"
    //     | "push-sum" ->
    //         printfn "push-sum"
    //     | _ ->
    //         errorFlag <- true
    //         printfn "ERROR: Algorithm not present"
    match topology with
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
            printfn "imp2D"
        | _ -> 
            errorFlag <- true
            printfn "ERROR: Topology not present"
    if not errorFlag then
        actorRef <! BossMessage 0

main(Environment.GetCommandLineArgs())
// If not for the below line, the program exits without printing anything.
// Please press any key once the execution is done and CPU times have been printed.
// You might have to scroll and see the printed CPU times. (Because of async processing)
System.Console.ReadKey() |> ignore
system.Terminate()
