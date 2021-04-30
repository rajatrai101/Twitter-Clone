module ServerMod

#load @"user.fs"
#load @"tweet.fs"
#load @"custom_types.fs"
#load @"global_data.fsx"

#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.FSharp
open CustomTypesMod
open GlobalDataMod
open UserMod


let mutable globalData = GlobalData()

let userExists (username:string) = 
    globalData.Users.ContainsKey username

let checkUserLoggedIn (username: string) =
    globalData.IsUserLoggedIn username

let signUpUser (username: string) (password: string) = 
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
    response, status

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
    response, status


let distributeTweet (username: string) (content: string) (isRetweeted: bool) (parentTweetId: int) =
    let mutable response, status, tweetID = "", false, -1
    if not (userExists username) then
        response <- "Error: User " + username + " does not exist in the database."
    elif not (globalData.LoggedInUsers.Contains username) then
        response <- "Error: User " + username + " is not logged in." 
    else
        if not isRetweeted then
            tweetID <- globalData.AddTweet content username
            response <- "Tweet registered successfully"
        else
            tweetID <- globalData.AddReTweet content username parentTweetId
            response <- "ReTweet registered successfully"
        status<-true
    response, status, tweetID

let signOutUser (username:string) = 
    let mutable response, status = "", false
    if not (globalData.LoggedInUsers.Contains username) then
        response<- "User is either not an valid user of not logged in."
    else
        globalData.MarkUserLoggedOut username
        response<- "User logged out successfully."
        status <- true
    response, status

let followAccount (followerUsername: string) (followedUsername: string) =
    let mutable response, status = "", false
    globalData.Users.[followedUsername].AddToFollowers(followerUsername)
    globalData.Users.[followerUsername].AddToFollowings(followedUsername)
    response <- "User " + followerUsername + " started following user " + followedUsername
    status <- true
    response, status

let findHashtags (username: string)(searchTerm: string) =
    let mutable response, status = "", false
    let data  = [|for tweetId in globalData.Hashtags.[searchTerm] do yield (globalData.Tweets.[tweetId].Creator ,globalData.Tweets.[tweetId].Content)|]
    status <- true
    response <- "Successfully retrieved " + string data.Length + " tweets."
    response, status, data

let findTweets (username: string) (searchType: QueryType) =
    let mutable response, status = "", false
    match searchType with
    | QueryType.MyMentions ->
        if (globalData.Users.ContainsKey username) && (globalData.LoggedInUsers.Contains username) then
            status <- true
            let data = [|for tweetId in globalData.Users.[username].MentionedTweets do yield (globalData.Tweets.[tweetId].Creator ,globalData.Tweets.[tweetId].Content)|]
            response <- "Successfully retrieved " + string data.Length + " tweets."
            response, status, data
        elif not (globalData.LoggedInUsers.Contains username) then
            status <- false
            response <- "User " + username + " is not logged in."
            response, status, [||]
        else
            status <- false
            response <-"User " + username + " does not exist in the database."
            response, status, [||]
    | QueryType.Subscribed ->
        if (globalData.Users.ContainsKey username) && (globalData.LoggedInUsers.Contains username) then
            status <- true
            let data = [|for tweetId in globalData.Users.[username].Tweets do yield (globalData.Tweets.[tweetId].Creator ,globalData.Tweets.[tweetId].Content)|]
            response <- "Successfully retrieved " + string data.Length + " tweets."
            response, status, data
        elif not (globalData.LoggedInUsers.Contains username) then
            status <- false
            response <- "User " + username + " is not logged in."
            response, status, [||]
        else
            status <- false
            response <-"User " + username + " does not exist in the database."
            response, status, [||]
// ------------------------------------------------------------------------------

let Server (mailbox: Actor<_>) =
    let mutable loggedInUserToClientMap: Map<string, IActorRef> = Map.empty
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        match msg with
        | SignUp(username,pwd) ->
            let response, status = signUpUser username pwd
            mailbox.Sender() <! ApiResponse(response, status)
        | SignIn(username, pwd) ->
            let response, status = signInUser username pwd
            if status then
                loggedInUserToClientMap <- loggedInUserToClientMap.Add(username, mailbox.Sender())
            mailbox.Sender() <! ApiResponse(response, status)
        | SignOut(username) ->
            let response, status = signOutUser username
            if status then
                loggedInUserToClientMap <- loggedInUserToClientMap.Remove(username)
            mailbox.Sender() <! ApiResponse(response, status)
        | RegisterTweet(senderUser, content) ->
            let response, status, tweetID = distributeTweet senderUser content false -1
            if status then
                let tweet = globalData.Tweets.[tweetID]
                for mentioned in tweet.Mentions do
                    if globalData.IsUserLoggedIn mentioned then
                        loggedInUserToClientMap.[mentioned] <! LiveFeed(tweet.Creator, tweet.Content)
                let followers = globalData.Users.[tweet.Creator].Followers
                for follower in followers do
                    if globalData.IsUserLoggedIn follower then
                        loggedInUserToClientMap.[follower] <! LiveFeed(tweet.Creator, tweet.Content)
            mailbox.Sender() <! ApiResponse(response, status)
        | RegisterReTweet(senderUser, content, subjectTweetId) ->
            let response, status, tweetID = distributeTweet senderUser content true subjectTweetId
            if status then
                let tweet = globalData.Tweets.[tweetID]
                let followers = globalData.Users.[tweet.Creator].Followers
                for follower in followers do
                    if globalData.IsUserLoggedIn follower then
                        loggedInUserToClientMap.[follower] <! LiveFeed(tweet.Creator, tweet.Content)
            mailbox.Sender() <! ApiResponse(response, status)
        | Follow(follower, followed) ->
            let response, status = followAccount follower followed
            mailbox.Sender() <! ApiResponse(response, status)
        | FindHashtags(username, searchTerm) ->
            let response, status, data = findHashtags username searchTerm
            mailbox.Sender() <! ApiDataResponse(response, status, data)
        | FindSubscribed(username) ->
            let response, status, data = findTweets username Subscribed
            mailbox.Sender() <! ApiDataResponse(response, status, data)
        | FindMentions(username) ->
            let response, status, data = findTweets username MyMentions
            mailbox.Sender() <! ApiDataResponse(response, status, data)
        | ShowData ->
            printfn "%A\n%A\n%A\n%A" globalData.Users globalData.LoggedInUsers globalData.Tweets globalData.Hashtags
        | _ -> failwith "Error!"
        return! loop()
    }
    loop()
