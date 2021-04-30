module ClientMod

#load @"custom_types.fs"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit"
#r "nuget: FSharp.Data"

open System
open Akka.FSharp
open CustomTypesMod
open System.Net.WebSockets;
open Akka.Actor
open Akka.FSharp
open System.Text;


// open ServerMod

open FSharp.Data

let twitterHostUrl = "http://localhost:8080"

let loginUrl = twitterHostUrl + "/login"
let logoutUrl = twitterHostUrl + "/logout"
let signUpUrl = twitterHostUrl + "/register"
let socketUrl = twitterHostUrl + "/websocket"
let followUrl = twitterHostUrl + "/follow"
let registerTweetUrl = twitterHostUrl + "/postTweet"
let registerReTweetUrl = twitterHostUrl + "/postReTweet"
let myMentionsUrl = twitterHostUrl + "/myMentions"
let mySubscriptionsUrl = twitterHostUrl + "/mySubscriptions"
let hashtagTweetsUrl = twitterHostUrl + "/hashtagTweets"


let system = ActorSystem.Create("TwitterClient"+string(System.Random().Next()))

let TwitterApiCall (url: string) (method: string) (body: string) = 
    match method with
    | "GET" ->
        Http.RequestString(url, httpMethod = method)
    | "POST" ->
        Http.RequestString(url, httpMethod = "POST", body = TextRequest body)
    | _ -> "invalid rest method"

type ClientMsgs =
    | Listen of ClientWebSocket

let create() = new ClientWebSocket()

let connect (port: int) (uid: string) (ws: ClientWebSocket) = 
    let tk = Async.DefaultCancellationToken
    Async.AwaitTask(ws.ConnectAsync(Uri(sprintf "ws://127.0.0.1:%d/websocket/%s" port uid), tk))

let close (ws: ClientWebSocket) =
    let tk = Async.DefaultCancellationToken
    let tsk = ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close", tk)
    while tsk.IsCompleted do
        ()

let send (str: string) (ws: ClientWebSocket) =
    let req = Encoding.UTF8.GetBytes str
    let tk = Async.DefaultCancellationToken
    Async.AwaitTask(ws.SendAsync(ArraySegment(req), WebSocketMessageType.Text, true, tk))

let read (ws: ClientWebSocket) = 
    let loop = true
    async {
        while loop do
            let buf = Array.zeroCreate 4096
            let buffer = ArraySegment(buf)
            let tk = Async.DefaultCancellationToken
            let r =  Async.AwaitTask(ws.ReceiveAsync(buffer, tk)) |> Async.RunSynchronously
            if not r.EndOfMessage then failwith "too lazy to receive more!"
            let resp = Encoding.UTF8.GetString(buf, 0, r.Count)
            if resp <> "" then
                printfn "WebSocket: %s" resp
            else
                printfn "Received empty response from server"
    } |> Async.Start

let SocketListener (mailbox: Actor<_>) =
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        // printfn "    Kuch to aya"
        match msg with
        | Listen(ws) ->
            // Socket Started Listening"
            read ws
        return! loop()
    }
    loop()

let connectAndPing (ws: ClientWebSocket)(port: int) (uid:string)= async {
    do! connect port uid ws
    System.Threading.Thread.Sleep(100)    
    do! send ("Hello from "+uid) ws
    let listener = spawn system "listener" SocketListener
    listener <! Listen(ws)
}

let Client (mailbox: Actor<_>) =
    let mutable id,pwd="",""
    let ws = new ClientWebSocket()
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        match msg with
        | Register(username, password) ->
            id <- username
            pwd <- password
            let resp = TwitterApiCall signUpUrl "POST" (sprintf """{"Arg1":"%s", "Arg2":"%s", "Arg3":"%s"}""" id pwd "")
            mailbox.Sender() <! resp
        | Login -> 
            let resp = TwitterApiCall loginUrl "POST" (sprintf """{"Arg1":"%s", "Arg2":"%s", "Arg3":"%s"}""" id pwd "")
            mailbox.Sender() <! resp
            select ("/user/"+id) system <! InitSocket
        | InitSocket -> 
            let resp = TwitterApiCall (socketUrl + "/" + id) "GET" ""
            connectAndPing ws 8080 id |> Async.RunSynchronously
            mailbox.Sender() <! resp
        | Logout -> 
            let resp = TwitterApiCall logoutUrl "POST" (sprintf """{"Arg1":"%s", "Arg2":"%s", "Arg3":"%s"}""" id "" "")
            printfn "Websocket: Disconnected!"
            mailbox.Sender() <! resp
        | GetMentions ->
            let resp = TwitterApiCall myMentionsUrl "POST" (sprintf """{"Arg1":"%s", "Arg2":"%s", "Arg3":"%s"}""" id "" "")
            mailbox.Sender() <! resp
        | GetSubscribed ->
            let resp = TwitterApiCall mySubscriptionsUrl "POST" (sprintf """{"Arg1":"%s", "Arg2":"%s", "Arg3":"%s"}""" id "" "")
            mailbox.Sender() <! resp
        | GetHashtags(searchTerm) ->
            let resp = TwitterApiCall hashtagTweetsUrl "POST" (sprintf """{"Arg1":"%s", "Arg2":"%s", "Arg3":"%s"}""" searchTerm "" "")
            mailbox.Sender() <! resp
        | SendTweet(content) ->
            let resp = TwitterApiCall registerTweetUrl "POST" (sprintf """{"Arg1":"%s", "Arg2":"%s", "Arg3":"%s"}""" id content "")
            mailbox.Sender() <! resp
        | SendReTweet(content,retweetId) ->
            let resp = TwitterApiCall registerReTweetUrl "POST" (sprintf """{"Arg1":"%s", "Arg2":"%s", "Arg3":"%s"}""" id content (string(retweetId)))
            mailbox.Sender() <! resp
        | FollowUser(followed) ->
            let resp = TwitterApiCall followUrl "POST" (sprintf """{"Arg1":"%s", "Arg2":"%s", "Arg3":"%s"}""" id followed "")
            mailbox.Sender() <! resp
        // | _ ->
        //     printfn "invalid match"
        return! loop()
    }
    loop()