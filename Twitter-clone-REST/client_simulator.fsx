#load @"custom_types.fs"
#load @"client.fsx"

#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit"
#r "nuget: MathNet.Numerics"
open System

open Akka.Actor
open Akka.FSharp
open CustomTypesMod
open ClientMod
open MathNet.Numerics

let zipfErrorPct = 10.00
let random = System.Random()
// let system = ActorSystem.Create("TwitterClient")

let precisionVal (num: int) (precisionPct: float) =
    // provides a value which is within a precision of (precisionPct %) from num
    // for example a sample value within 10% precision 100 could be 104 or 97.

    let random = System.Random()
    let low = int(float(num) * ((100.00 - precisionPct)/100.00))
    let hi = int(float(num) * ((100.00 + precisionPct)/100.00))
    random.Next(low, hi)

let nextZipfSize currFollowingSize = 
    // In general, the lower bound of this method is 1.
    // For instance, in case when the following size comes to be 0, max will pull it up to 1 which we assume
    // as the least size of subscibersip for any user.
    max 1 (precisionVal (currFollowingSize/2) zipfErrorPct)


let randomStr = 
    let chars = "abcdefghijklmnopqrstuvwxyz"
    let charsLen = chars.Length

    fun len -> 
        let randomChars = [|for i in 0..len -> chars.[random.Next(charsLen)]|]
        System.String(randomChars)


let getRandomUser (currentUser:int) (totalUsers: int): int =
    // get random user index other than the current user from the system
    let mutable randUserNum = random.Next(totalUsers)
    while randUserNum = currentUser do
        randUserNum <- random.Next(totalUsers)
    randUserNum

let getZipfDistribution (totalUsers: int)  (maxSubscribers: int) =
    let mutable initialDist: int array = Array.zeroCreate totalUsers
    Distributions.Zipf.Samples(initialDist, 1.5, maxSubscribers) 
    Array.sortBy (fun x -> (-x)) initialDist

let Simulator (mailbox: Actor<_>) =
    let mutable totalUsers=0
    let mutable maxSubscribers = 0
    let mutable maxTweets = 0
    let mutable tasksCount = 0
    let mutable tasksDone = 0
    let mutable stopWatch = null
    let mutable randomRetweetCount = 0
    let mutable randomLogoutCount = 0
    let mutable topProfilesCount = 0
    let mutable followingSizesZipf: int array = Array.empty
    let mutable tweetsDistZipf: int array = Array.empty
    let mutable tweetCountUsers: int array = Array.empty
    let mutable userActors: List<IActorRef> = List.empty
    let mutable caller: IActorRef = null
    let mutable userId: string = ""
    let mutable userPassword: string = ""

    let makeUserId (idNum:int): string = 
        let idSize = string(totalUsers).Length
        String.concat "" ["user"; String.replicate (idSize - string(idNum).Length) "0"; string(idNum)]
    
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        match msg with
        | Init ->
            caller <- mailbox.Sender()
            let mutable totalTasks = 0
            printfn "Welcome to Twitter Engine Simulation."
            printfn "Please provide a user name to begin with."
            let username = System.Console.ReadLine()
            userId <- username
            let userActor = spawn system userId Client
            printfn "Please set a secure password for your account."
            let pwd = System.Console.ReadLine()
            userPassword <- pwd
            printfn "Please wait while your account is being created..."
            System.Threading.Thread.Sleep(1000)
            let mutable task = userActor <? Register(userId, userPassword)
            let mutable resp = Async.RunSynchronously(task)
            printfn "Account creation status: %s" resp
            printfn "Do you want to login now? [Y/N]"
            let inp = System.Console.ReadLine()
            if inp = "" || inp.ToLower() = "y" || inp.ToLower() = "yes" then
                task <- userActor <? Login
                resp <- Async.RunSynchronously(task)
                printfn "Login status: %s" resp
                
                let mutable keepInteractive = true
                while keepInteractive do
                    
                    printfn "\n\nYou can perform the below operations."
                    printfn "[1] Tweet"
                    printfn "[2] ReTweet"
                    printfn "[3] Follow User"
                    printfn "[4] My Mentions"
                    printfn "[5] My Subscriptions"
                    printfn "[6] GetTweets by Hashtags"
                    printfn "[7] Logout\n\n"
                    
                    printfn "Please enter a number corresponding to the functionality you want to test:\n"
                    let mutable meth = System.Console.ReadLine()
                    
                    match meth with
                    | "1" -> // tweet
                        printfn "Feature: You can include hashtags (e.g., #sampleHashTag) and mentions (e.g., @sampleUserName) in your tweet."
                        printfn "What would you like to tweet?"
                        let tweetContent = System.Console.ReadLine()
                        printfn "Please wait while your tweet is being posted."
                        System.Threading.Thread.Sleep(500)
                        task <- userActor <? SendTweet(tweetContent)
                        resp <- Async.RunSynchronously(task)
                        printfn "Response: %s" resp
                    | "2" -> // retweet
                        printfn "Feature: You can write your own message on a retweet just like any other tweet."
                        printfn "Provide the ID of the tweet that you want to retweet:"
                        let rid = System.Console.ReadLine()
                        printfn "What message do you want to add to this retweet?"
                        let retweetContent = System.Console.ReadLine()
                        printfn "Please wait while your retweet is being posted."
                        System.Threading.Thread.Sleep(500)
                        task <- userActor <? SendReTweet(retweetContent, int(rid))
                        resp <- Async.RunSynchronously(task)
                        printfn "Response: %s" resp
                    | "3" -> // follow user
                        printfn "Provide the UserID of the user that you want to follow:"
                        let uid = System.Console.ReadLine()
                        printfn "Please wait while your request is being posted."
                        System.Threading.Thread.Sleep(500)
                        task <- userActor <? FollowUser(uid)
                        resp <- Async.RunSynchronously(task)
                        printfn "Response: %s" resp
                    | "4" -> // get mentions
                        printfn "===================My Mentions==================="
                        printfn "Please wait while your mentions are being fetched..."
                        System.Threading.Thread.Sleep(500)
                        task <- userActor <? GetMentions
                        resp <- Async.RunSynchronously(task)
                        printfn "===================================================="
                        printfn "Response: %s" resp
                    | "5" -> // get subscriptions
                        printfn "===================My Subscriptions==================="
                        printfn "Please wait while your subscriptions are being fetched..."
                        System.Threading.Thread.Sleep(500)
                        task <- userActor <? GetSubscribed
                        resp <- Async.RunSynchronously(task)
                        printfn "===================================================="
                        printfn "Response: %s" resp
                    | "6" -> // get hashtag tweets
                        printfn "Provide a hashtag to find tweets for."
                        let ht = System.Console.ReadLine()
                        printfn "===================Tweets for #%s===================" ht
                        printfn "Please wait while your subscriptions are being fetched..."
                        System.Threading.Thread.Sleep(500)
                        task <- userActor <? GetHashtags(ht)
                        resp <- Async.RunSynchronously(task)
                        printfn "===================================================="
                        printfn "Response: %s" resp

                    | "7" -> // logout
                        printfn "Please wait while you are being logged out..."
                        System.Threading.Thread.Sleep(500)
                        task <- userActor <? Logout
                        resp <- Async.RunSynchronously(task)
                        printfn "Response: %s" resp
                        mailbox.Sender() <! "Simulation Terminated."
                    |  _ ->
                        printfn "Invalid option selected."
            return! loop()
    }
    loop()

let main(args: array<string>) =
    let simulatorActor = spawn system "simulator" Simulator
    let task = simulatorActor <? Init
    let response = Async.RunSynchronously(task)
    printfn "%s" (string(response))
main(Environment.GetCommandLineArgs())