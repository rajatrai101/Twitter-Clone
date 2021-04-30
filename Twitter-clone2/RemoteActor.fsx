// RemoteActor.fsx
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
let config =
    Configuration.parse
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                hostname = ""127.0.0.1""
                port = 9191
            }
        }"
let system = System.create "RemoteFSharp" config
let echoServer = 
    spawn system "remoteecho"
    <| fun mailbox ->
        let rec loop() =
            actor {
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()
                printfn "echoServer called"
                match box message with
                | :? string -> 
                    printfn "kuch to"
                    sender <! sprintf "Echo: %s" message
                    return! loop()
                | _ ->  failwith "Unknown message"
            }
        loop()
Console.ReadLine() |> ignore