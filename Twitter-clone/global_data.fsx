module GlobalDataMod

#load @"user.fs"
#load @"tweet.fs"

open UserMod
open TweetMod


type GlobalData() =
    let mutable tweetAutoIncrement = 1
    let mutable connectedServers = 0
    let mutable users: Map<string, User> = Map.empty
    let mutable tweets: Map<int, Tweet> = Map.empty
    let mutable hashtags: Map<string, int[]> = Map.empty
    let mutable loggedInUsers: Set<string> = Set.empty

    let createUserInterval = 1000 //1000 milliseconds
    let createTweetInterval = 1000 //1000 milliseconds
    let createFollowerInterval = 0

    let privateAddConnectedServers count = 
        connectedServers <- connectedServers + count    

    let addHashtags (tweet: Tweet) = 
        for h in tweet.Hashtags do
            if hashtags.ContainsKey(h) then
                let updatedIds = Array.append hashtags.[h] [|tweet.Id|]
                hashtags <- hashtags.Add(h,updatedIds)
            else
                hashtags <- hashtags.Add(h,[|tweet.Id|])

    let updateMentions (tweet: Tweet) =
        for mentionedUser in tweet.Mentions do
            if users.ContainsKey mentionedUser then
                users.[mentionedUser].AddToMentionedTweets(tweet.Id)

    let privateIsUserLoggedIn (username: string) =
        loggedInUsers.Contains username

    let addTweetToFollowersTimeline (tweet: Tweet) =
        for follower in users.[tweet.Creator].Followers do
            users.[follower].AddToTimeline(tweet.Id)

    let addReTweetToOriginalUserTimeline (tweet: Tweet) =
        users.[tweets.[tweet.ParentTweetId].Creator].AddToTimeline(tweet.Id)

    let privateAddTweet (content: string) (username: string): int = 
        let tweet = Tweet(tweetAutoIncrement,username,content)
        tweetAutoIncrement <- tweetAutoIncrement + 1
        tweets <- tweets.Add(tweet.Id,tweet)
        users.Item(tweet.Creator).AddToTimeline(tweet.Id)
        users.Item(username).AddToTweets(tweet.Id)
        addTweetToFollowersTimeline tweet
        addHashtags tweet
        updateMentions tweet
        tweet.Id
    
    let privateAddReTweet (content: string) (username: string) (parentTweetId: int): int = 
        let tweet = Tweet(tweetAutoIncrement,username,content, parentTweetId)
        tweetAutoIncrement <- tweetAutoIncrement + 1
        tweets <- tweets.Add(tweet.Id,tweet)
        users.Item(tweet.Creator).AddToTimeline(tweet.Id)
        users.Item(username).AddToTweets(tweet.Id)
        addTweetToFollowersTimeline tweet
        addReTweetToOriginalUserTimeline tweet
        addHashtags tweet
        updateMentions tweet
        tweet.Id

    let privateAddUsers (username:string) (userObj: User) = 
        users <- users.Add(username, userObj)

    let privateMarkUserLoggedIn (username: string) =
        loggedInUsers <- loggedInUsers.Add(username)

    let privateMarkUserLoggedOut (username: string) =
        loggedInUsers <- loggedInUsers.Remove(username)
    
// =================== methods =======================
    member this.ConnectedServers
        with get() = connectedServers

    member this.Users
        with get() = users

    member this.Tweets
        with get() = tweets
    
    member this.Hashtags
        with get() = hashtags

    member this.LoggedInUsers
        with get() = loggedInUsers

    member this.AddServers count =
             count
    
    member this.AddUsers count =
        privateAddUsers count
    
    member this.AddTweet (content:string) (username: string) =
        privateAddTweet content username

    member this.AddReTweet (content:string) (username: string) (parentTweetId: int) =
        privateAddReTweet content username parentTweetId
    
    member this.MarkUserLoggedIn username =
        privateMarkUserLoggedIn username
    
    member this.MarkUserLoggedOut username =
        privateMarkUserLoggedOut username
    
    member this.IsUserLoggedIn (username: string): bool = 
        privateIsUserLoggedIn username
// ----------------------------------------------------------
