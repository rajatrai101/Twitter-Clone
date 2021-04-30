module CustomTypesMod

type PayloadArgs = {
    Arg1: string
    Arg2: string
    Arg3: string
}

type QueryType = 
    | MyMentions
    | Subscribed
    

type ServerApi = 
    | SignUp of string * string
    | SignIn of string * string
    | SignOut of string
    | RegisterTweet of string * string
    | RegisterReTweet of string * string * int
    | Follow of string * string
    | FindMentions of string 
    | FindSubscribed of string
    | FindHashtags of string
    | ShowData 

type ClientApi =
    | Login
    | Logout
    | InitSocket
    | GetMentions
    | GetSubscribed
    | GetHashtags of string
    | SendTweet of string
    | SendReTweet of string * int
    | Register of string * string
    | FollowUser of string

type InitEmulation =
    | Init