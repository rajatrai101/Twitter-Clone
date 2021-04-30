module ClientMod

#load @"custom_types.fs"
#load @"server.fsx"


#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.FSharp
open CustomTypesMod
open ServerMod

let system = ActorSystem.Create("Twitter")
// printfn "Inside Client: %A" system
let serverActor = spawn system "server" Server

// let serverActor = system.ActorSelection("akka.tcp://Twitter@127.0.0.1:9191/user/server")
// let serverActor = select "akka.tcp://Twitter@127.0.0.1:9191/user/server" system


// let Client (mailbox: Actor<_>) =
let Client (mailbox: Actor<_>) =
    let mutable id,pwd="",""
    // let serverActor = select "/user/server" serverActorRef
    // serverActor <! Testing
    // serverActorRef <! Testing
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        match msg with
        | Register(username, password) ->
            id <- username
            pwd <- password
            serverActor <! SignUp(id, pwd)
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
        | LiveFeed(tweetCreator, tweetContent) ->
            printfn "Live Update for @%s => @%s tweeted %s\n" id tweetCreator tweetContent
        | ApiResponse(response, status) ->
            if not status then
                printfn "Server: %s" response
            select "/user/simulator" system  <! UnitTaskCompleted
        | ApiDataResponse(response, status, data) ->
            printfn "=============+%s Query Response+=================" id
            for user, tweet in data do
                printfn "@%s : \" %s \"" user tweet
            printfn "============================================="
        // | _ ->
        //     printf "Maaaaoooo!!"
        return! loop()
    }
    loop()