module UserMod

type User(uId: string, password:string) = 
    // private immutable value

    let created = System.DateTime.Now
    // private mutable value
    let mutable followers: Set<string> = Set.empty
    let mutable following: Set<string> = Set.empty
    let mutable tweets: Set<int> = Set.empty
    let mutable timeLine: Set<int> = Set.empty
    let mutable mentionedTweets: Set<int> = Set.empty
    let password = password
    
    member this.Followers
        with get() = followers

    member this.Following
        with get() = following
    
    member this.Timeline
        with get() = timeLine
    
    member this.Tweets
        with get() = tweets

    member this.MentionedTweets
        with get() = mentionedTweets

    member this.Id = uId

    // private function definition
    member this.AddToFollowers followerUsername = 
        followers <- followers.Add(followerUsername)
    
    member this.AddToFollowings followedUsername = 
        following <- following.Add(followedUsername)

    // private function definition
    member this.AddToTimeline tweetId = 
        timeLine <- timeLine.Add(tweetId)

    member this.AddToTweets input = 
        tweets <- tweets.Add(input)

    member this.AddToMentionedTweets tweetId = 
        mentionedTweets <- mentionedTweets.Add(tweetId)
// // test
// let instance = User(69)
// instance.AddToFollowers ["loda";"lassan"] 
// instance.AddToTimeline ["lod";"assan"] 