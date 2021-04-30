module TweetMod
open System.Text.RegularExpressions;

type Tweet(tweetid: int, creator :string, content: string, ?parent) = 
    let hashtags = [for x in content.Split [|' '|] do if x.StartsWith('#') && x.Length>1  then yield x.Replace("#","")]
    let mentions = [for x in content.Split [|' '|] do if x.StartsWith('@') && x.Length>1  then yield x.Replace("@","")]
    let mutable parentTweetId = match parent with
                                | Some x -> x
                                | None -> -1
    
    member this.Id = tweetid
    member this.Created = System.DateTime.Now
    member this.Content = content
    member this.Creator = creator
    member this.Mentions 
        with get() = mentions
    member this.Hashtags
        with get() = hashtags
    member this.ParentTweetId
        with get() = parentTweetId