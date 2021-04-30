// RemoteActor.fsx
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
#r "nuget: Akka.Serialization.Hyperion"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Text.Json

let config =
    Configuration.parse
        @"akka {
            actor.serializers{
                json    = ""Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion""
                bytes   = ""Akka.Serialization.ByteArraySerializer""
            }
            actor.serialization-bindings {
                ""System.Byte[]""   = bytes
                ""System.Object""   = json
            }
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                hostname = ""127.0.0.1""
                port = 8792
                send-buffer-size = 5120000b
                receive-buffer-size = 5120000b 
                maximum-frame-size = 1024000b
                tcp-keepalive = on
            }
        }"
let system = System.create "RemoteFSharp" config
type TestRec={A:string;B:int}

let echoServer = 
    spawn system "remoteecho"
    <| fun (mailbox: Actor<_>) ->
        let rec loop() =
            actor {
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()
                printfn "echoServer called\nSender %A" sender
                match box message with
                | :? TestRec as msg -> 
                    // let stt = JsonSerializer.Deserialize<> message
                    printfn "Got: %A" msg
                    let v = 1
                    // system.ActorSelection("akka.tcp://RemoteFSharp@127.0.0.1:8002/user/local") <! message
                    sender <! message
                    printfn "Send back: %s" message
                    return! loop()
                | _ ->  failwith "Unknown message"
            }
        loop()
Console.ReadLine() |> ignore