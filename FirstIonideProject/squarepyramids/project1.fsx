// #time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.TestKit
open System.Diagnostics
open System.Collections.Generic

// This is the size of worker actors' pool. At any given point in time there are at max these many actors utilizing the CPU
let totalWorkers = 27
let mutable N_copy:uint64 = 0UL
let resList = new List<uint64>()
// Calculates the sum of squares of first n natural numbers
let firstNSquare (n: uint64) : uint64 = ((n*(n+1UL)*(2UL*n+1UL))/6UL)

// Calculates the sum of squares of the numbers in the range a to b
let sumSquare (a:uint64) (b:uint64) : uint64 = 
    let res1 = firstNSquare b 
    let res2 = firstNSquare (a-1UL)
    res1 - res2

// Verifies if the sum of squares from a to b is a perfect square.
let isSumPerfectSquare (a:uint64) (b:uint64) : bool = 
    let sqt = sumSquare a b |> float |> sqrt
    sqt = floor sqt

// This method pre-calculates the total number of workers going to be used from the worker actors' pool based on the given input
let totalRunningWorkers (N:uint64) = 
    let pws = uint64(ceil(float N / float totalWorkers))
    let mutable c = 0
    for i in 1UL..pws..N do
        c <- c + 1
    c

let listPrinter (a: List<uint64>) = 
    for i in a do
        printf "%d, " i
    printfn ""

let system = ActorSystem.Create("FSharp")

// Message types Created for the Supervisor actor
type BossMessage = 
    // this message type takes the value of N and k
    | BossMessage of uint64 * uint64
    // This message type takes the value of the first number of Luca's pyramid sequence
    // If the value of first argument is passed as 0, it just means that the worker is informing the supervisor before exiting
    | WorkerTaskFinished of uint64 * int

// Message types created for worker actors
// This message type takes the information about the subproblem a worker gets in the form of start, end and k
type WorkerMessage = WorkerMessage of uint64 * uint64 * uint64

// *********** WORKER ACTOR LOGIC **********
let WorkerActor (mailbox: Actor<_>) =
    let rec loop() = actor {
        let! WorkerMessage(st, en, k) = mailbox.Receive()
        // For every number, check if it can form a Luca's pyramid of size k
        for firstNum = st to en do
            if isSumPerfectSquare firstNum (firstNum+k-1UL) then
                // if a luca's sequence is found, then inform supervisor with the current value of firstNum 
                mailbox.Sender() <! WorkerTaskFinished(firstNum, 1)
        // inform the supervisor after processing the supbroblem
        mailbox.Sender() <! WorkerTaskFinished(0UL, 1)
        return! loop()
    }
    loop()

// *************** SUPERVISOR ACTOR'S HELPER UTILITY **************
let supervisorHelper (N:uint64) (k: uint64) = 
    // perWorkerSegment variable is responsible for dividing the work equally among (most of) the worker actors.
    let perWorkerSegment = uint64(ceil(float N / float totalWorkers))
    printfn "Subproblems per worker = %d" perWorkerSegment

    // Initialize the worker actors' pool containing the references of all the worker actors.
    let workersList = List.init totalWorkers (fun workerId -> spawn system (string workerId) WorkerActor)
    let mutable workerIdNum = 0;
    
    for i in 1UL .. perWorkerSegment .. N do
        // Ever worker is assigned a sub-problem of size perWoerketSegment
        // In case of an underflow, assign the remaining sub-problem to the last worker.
        workersList.Item(workerIdNum) <! WorkerMessage(i, min N (i+perWorkerSegment-1UL), k)
        workerIdNum <- workerIdNum + 1

// *********** SUPERVISOR ACTOR LOGIC **********
let SupervisorActor (mailbox: Actor<_>) = 
    // count keeps track of all the workers that finish their work and ping back to the supervisor
    let mutable count  = 0 

    // Logic for measuring CPU and real time
    let proc = Process.GetCurrentProcess()
    let cpu_time_stamp = proc.TotalProcessorTime
    let timer = new Stopwatch()
    timer.Start()
    let stopWatch = Diagnostics.Stopwatch.StartNew()
    // *****************************************

    let rec loop () = actor {
        let! msg = mailbox.Receive ()
        match msg with
        // Process main input
        | BossMessage(N, k) ->
            // Discard any invalid inputs
            if(k < 1UL || N < 1UL) then
                printfn "Please provide positive, non-zero integral values for N and K"
                printfn "Press any key to exit..."
            else
                supervisorHelper N k
        // Worker pings back after finishing the sub-problem
        | WorkerTaskFinished(st,c) -> 
            // If successfully finds the Luca's sequence (st > 0), then the print the provided first number (st)
            if c=1 && st > 0UL then
                // printfn "%d" st
                resList.Add(st)
            // Regardless, keep track of the workers who are done
            count <- count + c

            // If all the running workers are done, then stop recording the CPU and REAL time and output the metrics
            if count = (totalRunningWorkers N_copy) then
                printfn "========\nResults:"
                listPrinter resList
                printfn "================\n"
                timer.Stop()
                let cpu_time = (proc.TotalProcessorTime-cpu_time_stamp).TotalMilliseconds
                printfn "CPU time = %dms" (uint64 cpu_time)
                printfn "Real time = %dms" timer.ElapsedMilliseconds
                printfn "CPU to REAL time ratio = %f" (cpu_time/float timer.ElapsedMilliseconds)
        return! loop ()
    }
    loop ()


let main(args: array<string>) = 
    let N = uint64(args.[3])
    let k = uint64(args.[4])
    N_copy <- N
    // Spawnig supervisor actor
    let actorRef = spawn system "SupervisorActor" SupervisorActor
    printfn "Total workers = %d" (totalRunningWorkers N_copy)
    actorRef <! BossMessage(N, k)

main(Environment.GetCommandLineArgs())
// If not for the below line, the program exits without printing anything.
// Please press any key once the execution is done and CPU times have been printed.
// You might have to scroll and see the printed CPU times. (Because of async processing)
System.Console.ReadKey() |> ignore
