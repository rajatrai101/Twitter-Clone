module CustomTypesMod

#load @"tweet.fs"
open TweetMod

type QueryType = 
    | MyMentions
    | Subscribed

type ApiResponse = struct
    val Msg : string
    val Status : bool
   
    new (msg,status)=
        {Msg=msg;Status=status;}
end

type ApiDataResponse = struct
    val Msg : string
    val Status : bool
    val Data : array<string * string>
    new (msg,status,data)=
        {Msg=msg;Status=status;Data=data;}
end

type ServerApi = 
    | SignUp of string * string
    | SignIn of string * string
    | SignOut of string
    | RegisterTweet of string * string
    | RegisterReTweet of string * string * int
    | Follow of string * string
    | FindMentions of string 
    | FindSubscribed of string
    | FindHashtags of string * string
    | ShowData 
    | Testing

type ClientApi =
    | Login
    | Logout
    | GetMentions
    | GetSubscribed
    | GetHashtags of string
    | SendTweet of string
    | SendReTweet of string * int
    | Response of ApiResponse
    | DataResponse of ApiDataResponse
    | Register of string * string
    | FollowUser of string
    | LiveFeed of string * string