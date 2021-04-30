import akka.actor._
import collection.mutable.HashMap
import collection.mutable.HashSet
import scala.util.Random
object project3 extends App {
	val numNodes = args(0).toInt 
	val numRequests = args(1).toInt
	val numDigits:Integer = Math.ceil(Math.log(numNodes)/Math.log(16)).toInt
	
	
	val system = ActorSystem("PROJECT3")
	println("Network construction initiated")
	var nodeId:String=""
	var hexNum:String=""
	var len = 0
	var actor:ActorRef = null
	nodeId = "0"*numDigits
	actor = system.actorOf(Props[Peer])
	actor ! Initialize(nodeId,numDigits)
	Global.actorMap(nodeId) = actor
	for(i<-1 to numNodes-1){
	  if(i==numNodes/4)
	    println("25% of network constructed")
	  else if(i==numNodes/2)
	    println("50% of network constructed")
	  else if(i==numNodes*3/4)
	    println("75% of network constructed")
	  hexNum = Integer.toHexString(i)
	  len = hexNum.length()	  
	  nodeId = "0"*(numDigits-len) + hexNum  
	  actor = system.actorOf(Props[Peer])
	  
	  
	  actor ! Initialize(nodeId,numDigits)
	  Global.actorMap(nodeId) = actor
	  Global.actorMap("0"*numDigits) ! Join(nodeId,0)
	  
	  Thread.sleep(5)
	  
	}
	Thread.sleep(1000)
	println("Network is built")
	var actorsArray = Global.actorMap.keys.toArray
	println("Processing Requests")
	var k = 1
	var destinationId=""
	var ctr=0
	while(k<=numRequests){		
		for(sourceId<-actorsArray){
		  ctr+=1
		  destinationId = sourceId
		  while(destinationId==sourceId){
		    destinationId = actorsArray(Random.nextInt(numNodes))
		  }
		  //println(s"$sourceId has chosen $destinationId")
		  //Global.srcdst(sourceId) = destinationId
		  Global.actorMap(sourceId) ! Route(destinationId,sourceId,0)
		  Thread.sleep(5)
		}
		println(s"Each peer has performed $k requests")
		k+=1
	}
	
	//Global.actorMap("9d") ! Route("f3","9d",0)
	//Global.actorMap("50") ! "Print"
	
	Thread.sleep(1000)
	println("Requests processed")
	//println("Hops map is ")	
	var totalHopSize:Double = 0
	//Global.actorHopsMap.foreach {keyVal => println(keyVal._1+"->"+keyVal._2.mkString(" "))}//+" "+Global.srcdst(keyVal._1))}
	println("Computing average hop size")
	Global.actorHopsMap.foreach {keyVal => totalHopSize += keyVal._2(0)}
	//println("Hops map size is "+Global.actorHopsMap.keys.size)
	
	println("Avg Hop Size "+totalHopSize/Global.actorHopsMap.keys.size)
	
	System.exit(0)
}

object Global{
  var actorMap:HashMap[String,ActorRef] = HashMap()
  var actorHopsMap:HashMap[String,Array[Double]] = HashMap()
  var srcdst:HashMap[String,String] = HashMap()
}


class Peer extends Actor{
  var id:String=""
  var rows:Integer=0
  var cols:Integer = 16
  var prefix,suffix=""
  var routingTable=Array.ofDim[String](0,0)
  var leafSet:HashSet[String] = HashSet()
  var commonPrefixLength=0
  var currentRow=0
  def receive={
    case Initialize(i,d)=>
      id=i
      rows=d
      routingTable = Array.ofDim[String](rows,cols)      
      var itr=0
      var number = Integer.parseInt(i,16)
      var left,right=number
      while(itr<8){
        if(left==0)
          left = Global.actorMap.keys.size-1
        leafSet += left.toString
        itr += 1
        left -= 1
      }
      while(itr<16){
        if(right==Global.actorMap.keys.size-1)
          right=0
        leafSet += right.toString
        itr += 1
        right += 1
      }
      
    case Join(key,currentIndex)=>
      //var newNodeRoutingTable = Array.ofDim[String](rows,cols)
      var i,j=0
      var k = currentIndex
      while(key(i)==id(i))
        i+=1
      commonPrefixLength=i
      //println(s"Common prefix is $i")
      var routingRow:Array[String]=Array()
      while(k<=commonPrefixLength){
        routingRow = routingTable(k).clone
        routingRow(Integer.parseInt(id(commonPrefixLength).toString,16)) = id
        Global.actorMap(key) ! UpdateRoutingTable(routingRow)
        k+=1
      }
      var rtrow = commonPrefixLength
      var rtcol = Integer.parseInt(key(commonPrefixLength).toString,16)
      if(routingTable(rtrow)(rtcol)==null){
       
        routingTable(rtrow)(rtcol) = key
      }
      else{
        Global.actorMap(routingTable(rtrow)(rtcol)) ! Join(key,k)
      }
      
    case UpdateRoutingTable(r:Array[String])=>
      //println(s"$id received "+ r.mkString(" "))
      routingTable(currentRow) = r
      currentRow+=1
    case Route(key,source,hops)=>
      if(key==id){
        //println(s"Reached at $key")
        if(Global.actorHopsMap.contains(source)){
          
          var total=Global.actorHopsMap(source)(1)
          var avgHops = Global.actorHopsMap(source)(0)
          Global.actorHopsMap(source)(0) = ((avgHops*total)+hops)/(total+1)
          Global.actorHopsMap(source)(1) = total+1
        }
        else{
          Global.actorHopsMap(source) = Array(hops,1)
        }
        //println("Reached")
        //context.system.shutdown
      }
      else if(leafSet.contains(key)){
        Global.actorMap(key) ! Route(key,source,hops+1)
      }
      else{
	      //println(s"Route message received by $id")
	      var i,j=0
	      while(key(i)==id(i))
	        i+=1
	      commonPrefixLength=i
	      var check=0
	      var rtrow=commonPrefixLength
	      var rtcol = Integer.parseInt(key(commonPrefixLength).toString,16)
	      //while(routingTable(rtrow)(rtcol)==null)
	      //  rtcol-=1
	      if(routingTable(rtrow)(rtcol)==null)
	        rtcol=0
	      //println(rtrow+" "+rtcol)
	      Global.actorMap(routingTable(rtrow)(rtcol)) ! Route(key,source,hops+1)
      }  
    case "Print"=>
      println(s"Printing $id 's routing table")
      for(i<-0 to rows-1){
      for(j<-0 to cols-1){
        print(routingTable(i)(j)+" ")
      }
      println()
      }
     
  }
}

case class Initialize(id:String,digits:Int)
case class Route(key:String,source:String,hops:Int)
case class Join(nodeId:String,currentIndex:Int)
case class UpdateRoutingTable(rt:Array[String])