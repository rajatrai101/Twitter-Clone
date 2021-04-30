#load @"user.fs"
#load @"tweet.fs"
#load @"custom_types.fsx"
#load @"global_data.fsx"

#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#r "nuget: Akka.Remote"
#r "nuget: Akka.Serialization.Hyperion"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.Serialization
open CustomTypesMod
open GlobalDataMod
open UserMod
open TweetMod

let mutable globalData = GlobalData()

let userExists (username:string) = 
    globalData.Users.ContainsKey username

let checkUserLoggedIn (username: string) =
    globalData.IsUserLoggedIn username

let signUpUser (username: string) (password: string) = 
    printfn "adfadfasdfasldfalskdjf;alskdjf;alsdkj"
    let mutable response, status = "", false
    if userExists username then
        response <- sprintf "User %s already exists in the database." username
    elif username.Contains " " then
        response <- "Invalid characters found in username. Use only alphanumeric characters."
    else
        // All ok. Create user now.
        let newUserObj = User(username, password)
        globalData.AddUsers username newUserObj
        response <- sprintf "Added user %s to the database." username
        status <- true
    ApiResponse(response, status)

let signInUser (username:string) (password: string) =
    let mutable response, status = "", false
    if not (userExists username) then
        response <- sprintf "User %s does not exist in the database." username
    elif (globalData.LoggedInUsers.Contains username) then
        response <- sprintf "User %s is already logged in." username
    else
        globalData.MarkUserLoggedIn username
        response <- sprintf "User %s logged in." username
        status <- true
    ApiResponse(response, status)

// let notifyFollowers (tweetID:int) clientMap = 
//     let tweet = globalData.Tweets.[tweetID]
//     let followers = globalData.Users.[tweet.Creator].Followers
//     for follower in followers do
//         if globa: Map<string, IActorRef>lData.IsUserLoggedIn follower then
//             clientMap.[follower] <! LiveFeed(tweet)

let distributeTweet (username: string) (content: string) (isRetweeted: bool) (parentTweetId: int) =
    let mutable response, status, tweetID = "", false, -1
    if not (userExists username) then
        response <- "Error: User " + username + " does not exist in the database."
    elif not (globalData.LoggedInUsers.Contains username) then
        response <- "Error: User " + username + " is not logged in." 
    else
        // printfn "Tweet Info %d %A %A" tweet.Id tweet.Mentions tweet.Hashtags
        if not isRetweeted then
            tweetID <- globalData.AddTweet content username
            response <- "Tweet registered successfully"
        else
            tweetID <- globalData.AddReTweet content username parentTweetId
            response <- "ReTweet registered successfully"
        status<-true
    ApiResponse(response, status), tweetID

let signOutUser (username:string) = 
    let mutable response, status = "", false
    if not (globalData.LoggedInUsers.Contains username) then
        response<- "User is either not an valid user of not logged in."
    else
        globalData.MarkUserLoggedOut username
        response<- "User logged out successfully."
        status <- true
    ApiResponse(response, status)

let followAccount (followerUsername: string) (followedUsername: string) =
    let mutable response, status = "", false
    globalData.Users.[followedUsername].AddToFollowers(followerUsername)
    globalData.Users.[followerUsername].AddToFollowings(followedUsername)
    response <- "User " + followerUsername + " started following user " + followedUsername
    status <- true
    ApiResponse(response, status)

let findHashtags (username: string)(searchTerm: string) =
    let mutable response, status = "", false
    let data  = [|for tweetId in globalData.Hashtags.[searchTerm] do yield (globalData.Tweets.[tweetId].Creator ,globalData.Tweets.[tweetId].Content)|]
    status <- true
    response <- "Successfully retrieved " + string data.Length + " tweets."
    ApiDataResponse(response, status, data)

let findTweets (username: string) (searchType: QueryType) =
    let mutable response, status = "", false
    match searchType with
    | QueryType.MyMentions ->
        if (globalData.Users.ContainsKey username) && (globalData.LoggedInUsers.Contains username) then
            status <- true
            let data = [|for tweetId in globalData.Users.[username].MentionedTweets do yield (globalData.Tweets.[tweetId].Creator ,globalData.Tweets.[tweetId].Content)|]
            response <- "Successfully retrieved " + string data.Length + " tweets."
            ApiDataResponse(response, status, data)
        elif not (globalData.LoggedInUsers.Contains username) then
            status <- false
            response <- "User " + username + " is not logged in."
            ApiDataResponse(response, status, [||])
        else
            status <- false
            response <-"User " + username + " does not exist in the database."
            ApiDataResponse(response, status, [||])
    | QueryType.Subscribed ->
        if (globalData.Users.ContainsKey username) && (globalData.LoggedInUsers.Contains username) then
            status <- true
            let data = [|for tweetId in globalData.Users.[username].Tweets do yield (globalData.Tweets.[tweetId].Creator ,globalData.Tweets.[tweetId].Content)|]
            response <- "Successfully retrieved " + string data.Length + " tweets."
            ApiDataResponse(response, status, data)
        elif not (globalData.LoggedInUsers.Contains username) then
            status <- false
            response <- "User " + username + " is not logged in."
            ApiDataResponse(response, status, [||])
        else
            status <- false
            response <-"User " + username + " does not exist in the database."
            ApiDataResponse(response, status, [||])
// ------------------------------------------------------------------------------

let Server (mailbox: Actor<_>) =
    let mutable loggedInUserToClientMap: Map<string, IActorRef> = Map.empty
    printfn "kicfgt"
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        match msg with
        | SignUp(username,pwd) ->
            printfn "dhan dhana dhan1 %s %s" username pwd
            let response = signUpUser username pwd
            mailbox.Sender() <! Response(response)
        | SignIn(username, pwd) ->
            let response = signInUser username pwd
            if response.Status then
                loggedInUserToClientMap <- loggedInUserToClientMap.Add(username, mailbox.Sender())
            mailbox.Sender() <! Response(response)
        | SignOut(username) ->
            let response = signOutUser username
            if response.Status then
                loggedInUserToClientMap <- loggedInUserToClientMap.Remove(username)
            mailbox.Sender() <! Response(response)
        | RegisterTweet(senderUser, content) ->
            let response, tweetID = distributeTweet senderUser content false -1
            if response.Status then
                // notifyFollowers (tweetID, loggedInUserToClientMap)
                let tweet = globalData.Tweets.[tweetID]
                for mentioned in tweet.Mentions do
                    if globalData.IsUserLoggedIn mentioned then
                        loggedInUserToClientMap.[mentioned] <! LiveFeed(tweet.Creator, tweet.Content)
                let followers = globalData.Users.[tweet.Creator].Followers
                for follower in followers do
                    if globalData.IsUserLoggedIn follower then
                        loggedInUserToClientMap.[follower] <! LiveFeed(tweet.Creator, tweet.Content)
            mailbox.Sender() <! Response(response)
        | RegisterReTweet(senderUser, content, subjectTweetId) ->
            let response, tweetID = distributeTweet senderUser content true subjectTweetId
            if response.Status then
                // notifyFollowers (tweetID, loggedInUserToClientMap)
                let tweet = globalData.Tweets.[tweetID]
                let followers = globalData.Users.[tweet.Creator].Followers
                for follower in followers do
                    if globalData.IsUserLoggedIn follower then
                        loggedInUserToClientMap.[follower] <! LiveFeed(tweet.Creator, tweet.Content)
            mailbox.Sender() <! Response(response)
        | Follow(follower, followed) ->
            let response = followAccount follower followed
            mailbox.Sender() <! Response(response)
        | FindHashtags(username, searchTerm) ->
            let response = findHashtags username searchTerm
            mailbox.Sender() <! DataResponse(response)
        | FindSubscribed(username) ->
            let response = findTweets username Subscribed
            mailbox.Sender() <! DataResponse(response)
        | FindMentions(username) ->
            let response = findTweets username MyMentions
            mailbox.Sender() <! DataResponse(response)
        | ShowData ->
            printfn "%A\n%A\n%A\n%A" globalData.Users globalData.LoggedInUsers globalData.Tweets globalData.Hashtags
            printfn "%A" globalData.Users.["rajat.rai"].MentionedTweets
        | Testing -> 
            mailbox .Sender() <! sprintf "Echo: %s" "lehsun"
        // | _ ->
        //     printfn "server test"
        return! loop()
    }
    loop()

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
                port = 9191
                send-buffer-size = 5120000b
                receive-buffer-size = 5120000b 
                maximum-frame-size = 1024000b
                tcp-keepalive = on
            }
        }"

let system = System.create "Twitter" config
printfn "Inside driver: %A" system

let serverActor = spawn system "server" Server
System.Console.ReadKey() |> ignore