#load @"custom_types.fsx"
#load @"client.fsx"

#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#r "nuget: Akka.Remote" 
#r "nuget: Akka.Serialization.Hyperion"


open System

open Akka.Actor
open Akka.FSharp
open Akka.Serialization
open CustomTypesMod
open ClientMod

let randomStr = 
    let chars = "abcdefghijklmnopqrstuvwxyz"
    let charsLen = chars.Length
    let random = System.Random()

    fun len -> 
        let randomChars = [|for i in 0..len -> chars.[random.Next(charsLen)]|]
        System.String(randomChars)

let main(args: array<string>) =
    // let totalUsers = int(args.[3])
    // let idSize = string(totalUsers).Length

    // let makeUserId (idNum:int): string = 
    //     String.concat "" ["user"; String.replicate (idSize - string(idNum).Length) "0"; string(idNum)]

    // test user id generator
    // for i in [1..totalUsers] do
    //     printfn "%s" (makeUserId(i))
    // let userActors = List.init totalUsers (fun idNum -> spawn system (makeUserId(idNum)) Client)
    // let mutable clientActor2 = spawn system "client2" Client
   
    let mutable clientActor = spawn system "client" Client
    clientActor <! Register("akkisingh","1234")
   
    // clientActor2 <! Register("rajat.rai","1234")
    // clientActor <! Login
    // clientActor2 <! Login
    // clientActor <! SendTweet("balle balle * 100 #lazymonday @rajat.rai @bodambasanti")
    // System.Threading.Thread.Sleep(1000)
    // clientActor <! SendTweet("#1231 @123 @assd")
    // System.Threading.Thread.Sleep(1000)
    // clientActor2 <! SendTweet("#1231 #123123 #asdkfmewsdf")
    // System.Threading.Thread.Sleep(1000)
    // clientActor2 <! FollowUser("akkisingh")
    // System.Threading.Thread.Sleep(1000)
    // clientActor <! FollowUser("rajat.rai")
    // clientActor <! SendReTweet("@akkisingh is a good boy", 1)
    // clientActor2 <! SendTweet("@rajat.rai is also a good boy!")
    // clientActor2 <! SendTweet("@akkisingh is of age 25")
    // clientActor2 <! SendTweet("@akkisingh likes to play badminton.")
    // clientActor2 <! SendTweet("@akkisingh bahar chalega ghumne??")
    // clientActor <! GetHashtags("asdkfmewsdf")
    // clientActor <! GetMentions
    // clientActor <! GetSubscribed
    // clientActor2 <! GetHashtags("asdkfmewsdf")
    // clientActor2 <! GetMentions
    // clientActor2 <! GetSubscribed
    // clientActor <! Logout
    // clientActor2 <! Logout
    // System.Threading.Thread.Sleep(500)
    // serverActor <! ShowData
    0

main(Environment.GetCommandLineArgs())
System.Console.ReadKey() |> ignore