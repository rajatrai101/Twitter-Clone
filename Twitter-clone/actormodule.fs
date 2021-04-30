module ActorModules

#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.FSharp

let PrinterActor (mailbox: Actor<_>) =
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        match msg with
        | "ShowLeaf" ->
            printfn "Lassan hai bhai!"
        | _ ->
            printf ""
        return! loop()
    }
    loop()