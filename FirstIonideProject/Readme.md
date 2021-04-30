[//]: # (Defining some variables here.)
[1]: pastry.fsx

# Team Members
## Akshay Kumar
UFID: 46799946
## Rajat Rai
UFID: 14172127

### 
###
</br>

# What is working?
The program implements the network join and routing as described in the Pastry paper and encode the simple application that associates a key  same as the ids used in pastry) with a string. The program contains the following pastry APIs:
* Init
* Join
* Route
</br>

# How to run the program?
The program takes a total of three inputs in the order as mentioned.
* **N:** The total number of nodes in the network.
* **R:** The number of message requests to perform.

To run the code, navigate to the project root directory using the below command. The root directory is the one where the &nbsp;`project3.fsx`&nbsp; file resides.

```bash
cd /path/to/proj3/
```
Now, you can run the program using the below command:

```bash
dotnet fsi --langversion:preview project.fsx [N] [R]
```
Where `N` is an integer number </br>
`R` is a integer number

</br>

# Largest inputs that worked

The table below summarizes the max size of the input we could successfully run.
