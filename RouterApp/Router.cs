using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace RouterApp
{
    class Router
    {
        public struct RouterState
        {
            public RouterState(IPEndPoint endPoint)
            {
                this.endPoint = endPoint;
            }

            private IPEndPoint endPoint;
            public IPEndPoint EndPoint { get { return endPoint; } set { endPoint = value; } }
            public IPAddress Ip { get { return endPoint.Address; } set { endPoint.Address = value; } }
            public int Port { get { return endPoint.Port; } set { endPoint.Port = value; } }
        }

        public string Info { get { return $"\nRouter Info:" +
                    $"\nID: {serverId}" +
                    $"\nIP: {servers[serverId-1].Ip}" +
                    $"\nPORT: {servers[serverId - 1].Port}"; } }

        private UdpClient listener;

        public int ServerCount { get { return servers.Length; } }

        RouterState[] servers;

        int[,] table;

        int[] parents;

        int[] dist;

        int numEdges;
        int serverId;
        int packets = 0;       // counter for packets received  

        String[] receivedRow;


        public Router(int id = 1)
        {
            ReadTopFile(id);
            DisplayTopFile();
            DisplayTable();
            Console.WriteLine(Info);

            BellmanFord();
            Display();

            //TODO: start listener with file read port
            Run();
        }

        #region UDP
        public void OnReceive(IAsyncResult res)
        {
            (int id, UdpClient client) = ((int, UdpClient))res.AsyncState;
            listener = client;
            IPEndPoint endPoint = servers[id].EndPoint;

            try
            {
                byte[] bytes = listener.EndReceive(res, ref endPoint);
                string receiveString = Encoding.ASCII.GetString(bytes);
                packets++;
                receivedRow = receiveString.Split(' ');

                Console.WriteLine(string.Join(",", receivedRow));


                UpdateRow(int.Parse(receivedRow[0]), receivedRow);
                DisplayTable();

                Console.WriteLine($"Received: {receiveString}");

                listener.BeginReceive(new AsyncCallback(OnReceive), res.AsyncState);
            } catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
        }
        public void Send(RouterState destination, string msg)
        {
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            IPEndPoint ep = destination.EndPoint;

            byte[] sendbuf = Encoding.ASCII.GetBytes(msg);

            s.SendTo(sendbuf, ep);

            Console.WriteLine("Message sent to the broadcast address");
        }
        #endregion

        private void Run()
        {
            // initializes listeners.
            RouterState myServer = servers[serverId - 1];
            listener = new UdpClient(myServer.Port);

            Console.WriteLine($"listening for messages on port[{myServer.Port}] and ip[{myServer.Ip}]");
            listener.BeginReceive(new AsyncCallback(OnReceive), (serverId, listener));

            string line = "";

            Console.WriteLine("Type [help] for a list of commands...\n");

            while ((line = Console.ReadLine()) != null)
            {
                Console.WriteLine();

                switch (line)
                {
                    case "DVTEST":
                        {
                            DVA d1 = new DVA();
                            int[] updateRow2 = { int.MaxValue, 0, 2, int.MaxValue };
                            int[] updateRow3 = { int.MaxValue, 2, 0, 1 };
                            int[] updateRow4 = { 5, int.MaxValue, 1, 0 };
                            d1.ReadTopFile();
                            d1.UpdateRow(2, updateRow2);
                            d1.UpdateRow(3, updateRow3);
                            d1.UpdateRow(4, updateRow4);
                            d1.DVectorAlg();
                            d1.Display();
                            d1.DisplayRouteArray();

                            break;
                        }

                    case "help":
                        {
                            Console.WriteLine("Commands:\n" +
                                "\n" +
                                "server -t <topology-file-name> -i <routing-update-interval> topology-file-name:" +
                                "\n" +
                                "\tThe topology file contains the initial topology configuration for the server, " +
                                "e.g., timberlake_init.txt. Please adhere to the format described in 3.1 for your " +
                                "topology files." +
                                "\n" +
                                "\n" +
                                "routing-update-interval:" +
                                "\n" +
                                "\tIt specifies the time interval between routing updates in seconds." +
                                "\n" +
                                "\n" +
                                "port and server-id:" +
                                "\n" +
                                "\tThey are written in the topology file. The server should find its port and " +
                                "server-id in the topology file without changing the entry format or adding any new " +
                                "entries." +
                                "\n" +
                                "\n" +
                                "update <server-ID1> <server-ID2> <Link Cost> server-ID1, server-ID2:" +
                                "\n" +
                                "\tThe link for which the cost is being updated." +
                                "\n" +
                                "\n" +
                                "Link Cost: It specifies the new link cost between the source and the destination " +
                                "server. Note that this command will be issued to both server-ID1 and server-ID2 " +
                                " and involve them to update the cost and no other server." +
                                "\n" +
                                "For example:" +
                                "\n" +
                                "\tupdate 1 2 inf: The link between the servers with IDs 1 and 2 is assigned to " +
                                "infinity." +
                                "\n" +
                                "\tupdate 1 2 8: Change the cost of the link to 8." +
                                "\n" +
                                "\n" +
                                "step:" +
                                "\n" +
                                "\tSend routing update to neighbors right away. Note that except this, routing " +
                                "updates only happen periodically." +
                                "\n" +
                                "\n" +
                                "packets:" +
                                "\n" +
                                "\tDisplay the number of distance vector (packets) this server has received since " +
                                "the last invocation of this information." +
                                "\n" +
                                "\n" +
                                "display:" +
                                "\n" +
                                "\tDisplay the current routing table. And the table should be displayed in a sorted " +
                                "order from small ID to big. The display should be formatted as a sequence of lines, " +
                                "with each line indicating: <source-server-ID> <next-hop-ID> <cost-of-path>" +
                                "\n" +
                                "\n" +
                                "disable <server-ID>:" +
                                "\n" +
                                "\tDisable the link to a given server. Doing this \"closes\" the connection to a " +
                                "given server with server-ID. Here you need to check if the given server is its " +
                                "neighbor." +
                                "\n" +
                                "\n" +
                                "crash:" +
                                "\n" +
                                "\t\"Close\" all connections. This is to simulate server crashes. Close all " +
                                "connections on all links. The neighboring servers must handle this close correctly " +
                                "and set the link cost to infinity."
                                );
                            break;
                        }
                    case var val when new Regex(@"^send\s+(\d{1})\s+(.+)$").IsMatch(val):
                        {
                            var m = new Regex(@"^send\s+(\d{1})\s+(.+)$").Match(line);
                            int id = Int32.Parse(m.Groups[1].Captures[0].Value);
                            string msg = m.Groups[2].Captures[0].Value;

                            if (msg.Length > 100)
                            {
                                Console.WriteLine("Error: message must be 100 characters or less");
                            }
                            else
                            {
                                Send(servers[id - 1], msg);
                                Console.WriteLine("Message sent to peer with id " + id.ToString());
                            }
                            break;
                        }
                    case "packets":
                        {
                            Console.WriteLine($"Total packets received: {packets}");
                            break;
                        }
                    case var val when new Regex(@"^update\s(\d{1,3})\s(\d{1,3})\s(\∞|\d)$").IsMatch(val):

                        {
                            var m = new Regex(@"^update\s(\d{1,3})\s(\d{1,3})\s(\∞|\d{1,3})$").Match(line);
                            int servEdge = Int32.Parse(m.Groups[1].Captures[0].Value);
                            int endEdge = Int32.Parse(m.Groups[2].Captures[0].Value);
                            int linkCost;
                            String tmpLinkCost = m.Groups[3].Captures[0].Value;
                            if (tmpLinkCost.Equals("∞"))
                            {
                                linkCost = int.MaxValue;
                            } else
                            {
                                linkCost = int.Parse(tmpLinkCost);
                            }

                            Console.WriteLine($"values pulled {servEdge} {endEdge}");
                            Console.WriteLine($"link cost is {linkCost}");

                            UpdateEdge(servEdge, endEdge, linkCost);
                            break;
                        }
                

                    case "crash":
                        {

                            Console.WriteLine("CRASH occured: links set to infinity");
                            crash();
                            break;
                        }
                    case "display":
                        {

                           Display();
                            break;
                        }

                    case var val when new Regex(@"^disable\s(\d{1,3})$").IsMatch(val):
                        {
                            var m = new Regex(@"^disable\s(\d{1,3})$").Match(line);
                            int disabledServer = Int32.Parse(m.Groups[1].Captures[0].Value);

                            if (disabledServer < 1 || disabledServer > ServerCount)
                            {
                                Console.WriteLine("the serer you inputed does not exist");
                            }
                            else if (table[serverId - 1, disabledServer - 1] == int.MaxValue)
                                {
                                    Console.WriteLine("You are not a neigbor with this server");
                                }
                                else
                                {

                                    table[serverId - 1, disabledServer - 1] = 0;
                                }
                            

                            Display();
                            break;
                        }
                    case "exit":
                        {
                            Console.WriteLine("All connections closing, good bye...");
                            /* end listener */
                            /**/
                            Console.WriteLine("Terminating connections");
                            return;
                        }
                    default:
                        {
                            Console.WriteLine("Error: Invalid Command, type [help] for a list of commands...");
                            break;
                        }
                }
                Console.WriteLine();
            }
            listener.Close();
        }

        #region fileIO
        public void Setup(int servCount)
        {
            table = new int[servCount, servCount];
            dist = new int[servCount];
            parents = new int[servCount];

            for (int i = 0; i < table.GetLength(0); i++)
            {

                dist[i] = int.MaxValue;

                for (int j = 0; j < table.GetLength(1); j++)
                {
                    // set all entries to effectively infinity 
                    table[i, j] = int.MaxValue;
                }

            }
            dist[0] = 0;
        }

        // TODO: refactor file string reading code.
        // reads the Topology file, and sets up everything
        public void ReadTopFile(int id)
        {

            try
            {
                StreamReader sr = new StreamReader($"Topology{id}.txt");
                int numServers = int.Parse(sr.ReadLine());
                numEdges = int.Parse(sr.ReadLine());

                // initialize the the array that holds ip and port info for each server
                servers = new RouterState[numServers];
                // read the ip address and ports of servers in top file
                for (int i = 0; i < servers.Length; i++)
                {
                    string[] servRow;
                    servRow = sr.ReadLine().Split(' ');
                    // don't need server id since servers are in order of id.
                    // servers[i].id = int.Parse(servRow[0]);
                    IPEndPoint ep = new IPEndPoint(IPAddress.Parse(servRow[1]), int.Parse(servRow[2]));
                    servers[i] = new RouterState(ep);
                }

                // setup routing table and prep for Distance vector algorithm 
                Setup(numServers);
                for (int j = 0; j < numEdges; j++)
                {
                    string[] row = sr.ReadLine().Split(' ');
                    serverId = int.Parse(row[0]);
                    int neighbor = int.Parse(row[1]);
                    int weight = int.Parse(row[2]);
                    table[serverId - 1, neighbor - 1] = weight;
                }
                table[serverId - 1, serverId - 1] = 0; // self connections are set to zero
               } 
            catch(Exception e)
            {
                Console.WriteLine($"Topology file for server {id} does not exist");
            }
            }

        public void DisplayTopFile()
        {
            Console.WriteLine("\nTopFile:");
            Console.WriteLine(ServerCount);
            Console.WriteLine(numEdges);

            for (int i = 0; i < servers.GetLength(0); i++)
            {
                Console.WriteLine((i + 1) + " " + servers[i].Ip + " " + servers[i].Port);

            }

            for (int j = 0; j < ServerCount; j++)
            {
                if (serverId == (j + 1))
                {

                }
                else
                {
                    Console.WriteLine(serverId + " " + (j + 1) + " " + table[serverId - 1, j]);
                }
            }
        }
        #endregion

        public void DisplayTable()
        {
            Console.WriteLine("\nTable:");
            for (int i = 0; i < table.GetLength(0); i++)
            {
                for (int j = 0; j < table.GetLength(1); j++)
                {
                    //TODO: output grid format
                    if (table[i, j] < int.MaxValue)
                    {
                        Console.Write(table[i, j] + "\t\t");
                    }
                    else
                    {
                        Console.Write("Infinity" + "\t");
                    }
                }
                Console.WriteLine("");
            }
        }


        public void Display()
        {
            for (int i = 0; i < dist.GetLength(0); i++)
            {
                Console.WriteLine("Source-Server: " + (i + 1) + " Next-Hop: " + (parents[i] + 1) + " Cost of Path: " + dist[i]);
            }
        }


        public void UpdateRow(int rowNum, String[] newRowArr)
        {
            rowNum--;

                // start at 1 for newRowArr because newRowArr[0] is the row id 
                for (int i = 0; i < table.GetLength(0); i++)
                {
                    table[rowNum, i] = int.Parse(newRowArr[i + 1]);
                }
            
        }

        // update 1 2 7   where 1 is serverID, 2 is edgeID, and 7 is link cost 
        public void UpdateEdge(int inputId, int edgeId, int linkCost)
        {


            if ((inputId < 1 || inputId > ServerCount) || (edgeId < 1 || edgeId > ServerCount))
            {
                Console.WriteLine("Inputed server or edge does not exist!");
            }
            else
            {

               

                if (inputId == serverId)
                {
                    table[inputId - 1, edgeId - 1] = linkCost;
                    table[edgeId - 1, inputId - 1] = linkCost;

                    Console.WriteLine($"Edge {inputId} {edgeId} was set to {linkCost}");
                    DisplayTable();
                }
                else
                {
                    Console.WriteLine("You tried to update a value that is not involved");
                }
            }
        }

        public void crash()
        {
            for(int i = 0; i < table.GetLength(0); i++)
            {
                table[serverId - 1, i] = int.MaxValue;

            }
        }

        public void BellmanFord()
        {
            Console.WriteLine("\nDistance Vector: ");

            for (int k = 0; k < table.GetLength(0); k++)
            {
                for (int i = 0; i < table.GetLength(0); i++)
                {
                    for (int j = 0; j < table.GetLength(1); j++)
                    {
                        if ((dist[i] + table[i, j] < dist[j]) && (table[i, j] != int.MaxValue && dist[i] != int.MaxValue))
                        {

                            dist[j] = dist[i] + table[i, j];
                            parents[j] = i;
                            int test = dist[i] + table[i, j];
                            Console.WriteLine($"dist[{i}] is {dist[i]} table[{i},{j}] is {table[i, j]}");
                            Console.WriteLine($"new edge value is {test} at {i}, {j}");

                            // check if our servers row changed (serverid - 1) set a boolean to indicate this and later send this to all other servers
                            // after we send this to the other servers we will need to set the boolean back to false

                            if ((i + 1) == serverId)
                            {
                                Console.WriteLine("Table updated send this to all peers");
                            }
                        }
                    }
                }
            }


            // The following handles negative weight cycles
            // I dont think we need it but I have included it here
            for (int i = 0; i < table.GetLength(0); i++)
            {
                for (int j = 0; j < table.GetLength(1); j++)
                {
                    if (dist[i] + table[i, j] < dist[j] && (table[i, j] != int.MaxValue && dist[i] != int.MaxValue))
                    {
                        Console.WriteLine("Negative weight cycle found");
                    }
                }
            }

            // the result of running the algorithm
            Console.Write("Distance ");
            Console.WriteLine("[{0}]", string.Join(", ", dist));
            //Console.Write("Parent ");
            //Console.WriteLine("[{0}]", string.Join(", ", parent));
        }
    }
}
