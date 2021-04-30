# Twitter Clone Server/Client CLI with REST APIs and Websockets, using F#, Akka.net, and Suave Framework.
## Team Members: Akshay Kumar [UFID: 4679-9946] | Rajat Rai [UFID: 1417-2127]

#### Aim 
The goal of this project is to implement REST APIs and Websockets on top of the prior implemented Twitter engine in part 1 of the project. We are here using REST(GET/POST) for HTTP  calls and Websockets for live notifications to the client from the server. It is a Twitter clone with functionalities that mimic the real social networking service. It implements this using the concurrent programming model of F# [1], Akka Actors[2], and Sauave[3] server.

#### Architecture
The project has three key parts:
* The Client Simulator:
    * This module is an Akka actor that mimics the functionality of the logged-in application provided to the user by the service. For example, Twitter for Web/Android.
* The Sever:
    * This is the component that forms the communication bridge between various signed-up users. All the tweets, follow notifications, etc. crossover from one user to another via the server as the mediator.
* The Database:
    * This component stores all the data related to hashtags, tweets, and users and also procedures to perform while modifying/adding them(reduces most of the load off the server).

#### How to run the program?

To run the code, navigate to the project root directory using the below command. The root directory is the one where the &nbsp;`server.fs`&nbsp; file resides.

```bash
cd /path/to/Twitter-clone-REST/
```
Now, you can run the program using the below command:

##### Server
```bash
dotnet watch run
```

##### Client
```bash
dotnet fsi --langversion:preview client_simulator.fsx
```

#### Functionalities
We Provide the following functionalities:
* Sign up for a new user
* Login/Logout for an existing user
* Follow for existing users from A to B
* Tweet/Retweet by an existing user by using @[username]
* Support for using hashtags in tweets and retweets by using #[hashtag]
* Support for Live Updates for logged-in users, on:
    * Tweets by users they follow
    * Tweets mentioning them
* Support for querying tweets by an existing user based on:
    * Tweets they made
    * Tweets by hashtags
    * Tweets by users they follow
    * Tweets mentioning them
* Population of a user's timeline and mentions by leveraging the above functionality.

#### Implementation
We start by creating a new server actor instance and multiple client instances on different terminals. The client implementation offers a command-line interface to input user's inputs for all actions it can perform. Any user/client needs to be registered using a username and password and then assigned their respective followers (one-by-one, closer to the real thing). After this, a client can make Tweets/Retweets and even Queries  

#### Results/Demo
We can communicate between our Twitter server and client using REST and Websockets.
Link to demo(Youtube) [] 

#### Dependencies
* Akka.Cluster~>1.4.10
* Akka.FSharp~>1.4.12
* Akka.Remote~>1.4.12
* Akka.TestKit~>1.4.12
* MathNet.Numerics~>4.12.0
* Suave~>2.4.6

#### Tested and Dvelopend on OS
* GNU/Linux,Windows

#### References
[1] [F#](https://fsharp.org/docs/)
[2] [Akka.net](https://getakka.net/articles/intro/what-is-akka.html)
[3] [Suave](https://suave.io/)
