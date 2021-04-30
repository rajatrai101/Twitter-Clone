module CustomTypesMod

type QueryType = 
    | MyMentions
    | Subscribed

type ApiResponse(response: string, status: bool) =
    let responseMsg = response
    let responseStatus = status

    member this.Response
        with get() = responseMsg

    member this.Status
        with get() = responseStatus


type ApiDataResponse(response: string, status: bool, data: array<string * string>) =
    let responseMsg = response
    let responseStatus = status
    let responseData = data

    member this.Response
        with get() = responseMsg

    member this.Status
        with get() = responseStatus
    
    member this.Data
        with get() = responseData


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

type ClientApi =
    | Login
    | Logout
    | GetMentions
    | GetSubscribed
    | GetHashtags of string
    | SendTweet of string
    | SendReTweet of string * int
    | Register of string * string
    | FollowUser of string
    | LiveFeed of string * string
    | ApiResponse of string * bool
    | ApiDataResponse of string * bool * array<string * string>
    

type InitEmulation =
    | Init of int * int * int
    | UnitTaskCompleted
    | RunQuerySimulation


let randTweets =[|"The fish dreamed of escaping the fishbowl and into the #toilet where he saw his #friend go.";
"We have young #kids who often walk into our room at night for various reasons including clowns in the closet.";
"There are few things better in life than a slice of #pie.";
"The #beauty of the sunset was obscured by the industrial cranes.";
"The swirled #lollipop had issues with the pop rock #candy.";
"I caught my squirrel rustling through my #gym bag.";
"He was the type of guy who liked #Christmas lights on his house in the middle of July.";
"They desperately needed another drummer since the current one only knew how to play bongos.";
"With a #single flip of the coin; his life changed forever.";
"Just go ahead and press that button.";
"He went back to the video to see what had been recorded and was shocked at what he saw.";
"Warm #beer on a cold day isn't my idea of fun.";
"People generally approve of dogs eating #cat #food but not cats eating #dog food.";
"Seek #success; but always be prepared for random cats.";
"Nothing is as cautiously cuddly as a pet porcupine.";
"We should play with legos at #camp.";
"He used to get confused between #soldiers and shoulders; but as a military man; he now soldiers responsibility.";
"He learned the hardest lesson of his #life and had the scars; both physical and mental; to prove it.";
"I liked their first two albums but changed my mind after that charity gig.";
"He wondered if she would #appreciate his toenail collection.";
"She was the type of girl who wanted to live in a #pink house.";
"He waited for the stop sign to turn to a go sign.";
"He played the game as if his life depended on it and the #truth was that it did.";
"He was surprised that his immense #laziness was inspirational to others.";
"She learned that water bottles are no longer just to hold liquid; but they're also status symbols.";|]