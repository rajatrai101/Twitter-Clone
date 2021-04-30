//LocalActor.fsx
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
#r "nuget: Akka.Serialization.Hyperion"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Text.Json
let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                actor.serializers {
                    json    = ""Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion""
                    bytes   = ""Akka.Serialization.ByteArraySerializer""
                }
                
                actor.serialization-bindings {
                    ""System.Byte[]""   = bytes
                    ""System.Object""   = json
                }
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                deployment {
                    /remoteecho {
                        remote = ""akka.tcp://Twitter@127.0.0.1:8792""
                    }
                }
            }
            remote {
                helios.tcp {
                    port = 8002
                    hostname = ""127.0.0.1""
                }
            }
        }")
let system = ActorSystem.Create("RemoteFSharp", configuration)

let echoClient = system.ActorSelection("akka.tcp://RemoteFSharp@127.0.0.1:8792/user/remoteecho")

type TestRec={A:string;B:int}

let tt = {TestRec.A="GO TO SLEEP NOW";TestRec.B=1}

let local = 
    spawn system "local"
    <| fun (mailbox: Actor<_>) ->
        let rec loop() =
            actor {
                let! message = mailbox.Receive()
                printfn "Local :%A" message
                let sender = mailbox.Sender()
                match box message with
                | :? string ->
                    // let tts = JsonSerializer.Serialize(tt)
                    printf "Sending to server" 
                    echoClient <! tt
                    return! loop()
                | _ ->  
                    printfn "Unknown message"
                    return! loop()
            }
        loop()
local <! "F# is very bad!"

// let response = Async.RunSynchronously (task, 1000)
// printfn "Reply from remote %s" (string(response))
System.Console.ReadKey() |> ignore