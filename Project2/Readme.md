[//]: # (Defining some variables here.)
[1]: gossip.fsx

# Team Members:
## Akshay Kumar
UFID: 46799946
## Rajat Rai
UFID: 14172127

### 
###
</br>

# What is working?
The program implements the following two group communication/aggregation algorithms
* Asynchronous Gossip
* Push Sum

for the topologies mentioned below:
* **Full Network:** Every actor is a neighbor of all other actors. That is, every
actor can talk directly to any other actor.
* **Line:** Actors are arranged in a line. Each actor has only 2 neighbors (one
left and one right, unless you are the first or last actor).
* **2D Grid:** Actors form a 2D grid. The actors can only talk to the grid
neighbors.
* **Imperfect 2D Grid:** Grid arrangement but one random other neighbor
is selected from the list of all actors (4+1 neighbors).

</br>

# How to run the program?
The program takes a total of three inputs in the order as mentioned.
* **N:** The total number of nodes in the topology.
* **Topology:** The topology arrangement of the given nodes.
* **Algorithm:** The group communication alogrithm that user wants to simulate.

To run the code, navigate to the project root directory using the below command. The root directory is the one where the &nbsp;`gossip.fsx`&nbsp; file resides.

```bash
cd /path/to/proj2/
```
Now, you can run the program using the below command:

```bash
dotnet fsi --langversion:preview gossip.fsx [N] [topology] [algorithm]
```
Where `N` is an integer number </br>
`topology` can be one of 
* `full`: for full network.
* `imp2d`: for imperfect 2D grid
* `2d`: for 2D grid
* `line`: for a line topology

and algorithm can be one of `gossip` and `push-sum`.

</br>

# Largest inputs that worked

The table below summarizes the max size of input that we successfully ran for each of the combinations of the algorithms and the topologies.

|          |   Full  | Imperfect 2D |    2D   |   Line  |
|:--------:|:-------:|:------------:|:-------:|:-------:|
|  Gossip  | 1000000 |    1000000   | 1000000 | 1000000 |
| Push-sum | 1000000 |    1000000   | 1000000 |   100   |
|          |         |              |         |         |
