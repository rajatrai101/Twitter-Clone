module ClientMod

#load @"custom_types.fsx"
#load @"tweet.fs"

#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit"
#r "nuget: Akka.Remote"
#r "nuget: Akka.Serialization.Hyperion"



open System
open Akka.Actor
open Akka.FSharp
open Akka.Configuration
open Akka.Serialization
open TweetMod
open CustomTypesMod

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
                    /server {
                        remote = ""akka.tcp://Twitter@127.0.0.1:9191""
                    }
                }
            }
            remote {
                helios.tcp {
                    port = 8005
                    hostname = ""127.0.0.1""
                }
            }
        }")

let system = ActorSystem.Create("Twitter", configuration)
printfn "Inside Client: %A" system


let Client (mailbox: Actor<_>) =
    // let serverActor = select "akka.tcp://Twitter@127.0.0.1:9191/user/server" system
    let serverActor = system.ActorSelection("akka.tcp://Twitter@127.0.0.1:9191/user/server")
    printfn "%A" serverActor
    let mutable id,pwd ="",""
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        match msg with
        | Register(username, password) ->
            printf "bajenge dohol!"
            id <- username
            pwd <- password
            serverActor <! SignUp(id,pwd)
            // serverActor <! Testing
            printf "phir se bajenge dohol!"
        | Login -> 
            serverActor<! SignIn(id,pwd)
        | Logout -> 
            serverActor<! SignOut(id)
        | GetMentions ->
            serverActor <! FindMentions(id)
        | GetSubscribed ->
            serverActor <! FindSubscribed(id)
        | GetHashtags(searchTerm) ->
            serverActor <! FindHashtags(id, searchTerm)
        | SendTweet(content) ->
            serverActor<! RegisterTweet(id, content) 
        | SendReTweet(content,retweetId) ->
            serverActor<! RegisterReTweet(id, content, retweetId) 
        | FollowUser(followed) ->
            serverActor <! Follow(id,followed)
        | Response(response) -> 
            // if not response.Status then
            printfn "Server: %s" response.Msg
        | DataResponse(response) ->
            printfn "=============+%s Query Response+=================" id
            for user, tweet in response.Data do
                printfn "@%s : \" %s \"" user tweet
            printfn "============================================="
        | LiveFeed(tweetCreator, tweetContent) ->
            printfn "LIVEUPDATE for @%s >> %s tweeted \n ~~~~~~~~%s\n~~~~~~~~" id tweetCreator tweetContent
                
        return! loop()
    }
    loop()