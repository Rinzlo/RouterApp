using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 
///  How this works:
///  Call ReadTopFile() to read from the topology file and load up variables and arrays.
///  From here you can run the Distance vector algorithm by calling DVectorAlg().
///  To update rows call UpdateRow(in serverId, int[] newRowArray) where newRowArray is an array that contains new row info 
///  received from a UDP message and run DVectorAlg() again.  Then rinse and repeat  
///  
///  EX:
///  
///  int[] updateRow2 = { 7, 0, 2, int.MaxValue };
///  int[] updateRow3 = { 4, 2, 0, 1 };
///  int[] updateRow4 = { 3, int.MaxValue, 0, 0 };
///  DVA d1 = new DVA();
///  d1.ReadTopFile();
///  d1.UpdateRow(2, updateRow2);
///  d1.UpdateRow(3, updateRow3);
///  d1.UpdateRow(4, updateRow4);
///  d1.DVectorAlg();
///  d1.Display();
/// 
///  To Do:
///  Currently DVectorAlg() runs the routing algorithm and sets a boolean to indicate whether the servers row has been changed. 
///  This means that when that boolean is true we will have to send UDP messages to each server and run the UpdateRow() method
///  when a server receives one
/// 
///  Other Methods
///  Display():              // in requirements
///  DisplayRouteArray():    // not required
///  DisplayTop              // not required
/// 
/// 
/// </summary>

namespace RouterApp
{
    class DVA
    {

        // the routing table, fixed for 4 computers  
        int[,] routeArray;

        // set this serverID to be different in each server
        // the rest is information that is set from the Topology file 
        private const int serverId = 1;
        private int numServers;
        private int numEdges;
        ServerInfo[] servData;
        Boolean isUpdated = false;

        // the distance and next hop arrays are globals (Initialized everytime to on start and can change when updated)
        int[] dist = new int[4];
        int[] parent = new int[4];

        // this method is to be run when the array starts
        public void DVSetup(int numIndex)
        {
            routeArray = new int[numIndex, numIndex];

            for (int i = 0; i < routeArray.GetLength(0); i++)
            {

                dist[i] = int.MaxValue;

                for (int j = 0; j < routeArray.GetLength(1); j++)
                {
                    // set all entries to effectively infinity 
                    routeArray[i, j] = int.MaxValue;
                }

            }
            dist[0] = 0;
        }

        // initialized the array of servData objects for each server
        public void initServData(int numIndex)
        {
            servData = new ServerInfo[numServers];
            for (int i = 0; i < servData.Length; i++)
            {
                servData[i] = new ServerInfo();
            }

        }

        // update a row in the routeArray
        // run this when we get a new vector from a server 
        public void UpdateRow(int rowNum, int[] newRowArr)
        {

            rowNum--;

            if (rowNum >= 4 || rowNum == -1)
            {
                Console.WriteLine("row number in updateRow was out of bounds");
            }
            else if (newRowArr.GetLength(0) != 4)
            {
                Console.WriteLine("new row array in update row was not the appropriate size of 4");
            }
            else
            {
                for (int i = 0; i < routeArray.GetLength(0); i++)
                {
                    routeArray[rowNum, i] = newRowArr[i];
                }

            }

        }

        // just displays the route array for visual purposes
        public void DisplayRouteArray()
        {

            for (int i = 0; i < routeArray.GetLength(0); i++)
            {
                for (int j = 0; j < routeArray.GetLength(1); j++)
                {
                    if (routeArray[i, j] < int.MaxValue)
                    {
                        Console.Write(routeArray[i, j] + "          ");
                    }
                    else
                    {
                        Console.Write(routeArray[i, j] + " ");
                    }
                }
                Console.WriteLine("");
            }

        }

        // command stated in the requirements that prints out the table in the required way
        public void Display()
        {
            for (int i = 0; i < dist.GetLength(0); i++)
            {
                Console.WriteLine("Source-Server: " + (i + 1) + " Next-Hop: " + (parent[i] + 1) + " Cost of Path: " + dist[i]);
            }
        }

        // prints out the Topology file for visual purposes
        public void DisplayTopFile()
        {

            Console.WriteLine(numServers);
            Console.WriteLine(numEdges);

            for (int i = 0; i < servData.GetLength(0); i++)
            {
                Console.WriteLine((i + 1) + " " + servData[i].serverIP + " " + servData[i].port);

            }

            for (int j = 0; j < numServers; j++)
            {
                if (serverId == (j + 1))
                {

                }
                else
                {
                    Console.WriteLine(serverId + " " + (j + 1) + " " + routeArray[serverId - 1, j]);
                }
            }
        }

        // reads the Topology file, and sets up everything
        public void ReadTopFile()
        {
            StreamReader sr = new StreamReader("Topology.txt");
            numServers = int.Parse(sr.ReadLine());
            numEdges = int.Parse(sr.ReadLine());

            // initialize the the array that holds ip and port info for each server
            initServData(numServers);

            // read the ip address and ports of servers in top file
            for (int i = 0; i < servData.GetLength(0); i++)
            {
                String[] servStr;
                servStr = sr.ReadLine().Split(' ');
                servData[i].serverIP = servStr[1];
                servData[i].port = int.Parse(servStr[2]);
            }

            // setup routing table and prep for Distance vector algorithm 
            DVSetup(numServers);
            routeArray[serverId - 1, serverId - 1] = 0; // self connections are set to zero
            for (int j = 0; j < numEdges; j++)
            {
                String[] edgeValues;
                edgeValues = sr.ReadLine().Split(' ');
                routeArray[serverId - 1, int.Parse(edgeValues[1]) - 1] = int.Parse(edgeValues[2]);
            }
        }

        public void DVectorAlg()
        {
            for (int i = 0; i < routeArray.GetLength(0); i++)
            {
                for (int j = 0; j < routeArray.GetLength(1); j++)
                {
                    if ((dist[i] + routeArray[i, j] < dist[j]) && (routeArray[i, j] != int.MaxValue && dist[i] != int.MaxValue))
                    {

                        dist[j] = dist[i] + routeArray[i, j];
                        parent[j] = i;
                        int test = dist[i] + routeArray[i, j];
                        Console.WriteLine("dist[i] is " + dist[i] + " route[i,j] is " + routeArray[i, j]);
                        Console.WriteLine("new edge value is " + test + " at " + i + ", " + j);

                        // check if our servers row changed (serverid - 1) set a boolean to indicate this and later send this to all other servers
                        // after we send this to the other servers we will need to set the boolean back to false

                        if ((i + 1) == serverId)
                        {
                            isUpdated = true;
                            Console.WriteLine("Table updated send this to all peers");
                        }
                    }
                }
            }


            // The following handles negative weight cycles
            // I dont think we need it but I have included it here
            for (int i = 0; i < routeArray.GetLength(0); i++)
            {
                for (int j = 0; j < routeArray.GetLength(1); j++)
                {
                    if (dist[i] + routeArray[i, j] < dist[j] && (routeArray[i, j] != int.MaxValue && dist[i] != int.MaxValue))
                    {
                        Console.WriteLine("Negative weight cycle found");
                    }
                }
            }

            // the result of running the algorithm
            Console.Write("Distance ");
            Console.WriteLine("[{0}]", string.Join(", ", dist));
            Console.Write("Parent ");
            Console.WriteLine("[{0}]", string.Join(", ", parent));
        }
    }

    class ServerInfo
    {
        public String serverIP;
        public int port;
        public ServerInfo()
        {
            serverIP = "";
            port = 2000; // default port I guess ( above the reserved ports 0-1023)
        }
    }
}
