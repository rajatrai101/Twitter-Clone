module PrinterMod

#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.FSharp

let PrinterActor (mailbox: Actor<_>) =
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        printfn "%A" msg
        match box msg with
        | :? string ->
            printfn msg
        | _ ->
            printfn "Nothing to print to console."
        return! loop()
    }
    loop()