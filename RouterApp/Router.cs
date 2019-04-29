﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;

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

        public int ServerCount { get { return servers.Length; } }

        RouterState[] servers;

        int[,] table;

        int[] parents;

        int[] dist;

        int numEdges;
        int serverId;

        private static int listenPort;

        public Router(int id = 1)
        {
            ReadTopFile(id);
            listenPort = servers[serverId-1].Port;
            DisplayTopFile();
            DisplayTable();
            Console.WriteLine(Info);

            BellmanFord();
            

            //TODO: start listener with file read port
            Run();
        }

        #region UDP
        public void OnReceive(IAsyncResult res)
        {
            (int id, UdpClient listener) = ((int, UdpClient))(res.AsyncState);
            IPEndPoint endPoint = servers[id].EndPoint;

            try
            {
                byte[] bytes = listener.EndReceive(res, ref endPoint);
                string receiveString = Encoding.ASCII.GetString(bytes);

                Console.WriteLine($"Received: {receiveString}");

                listener.BeginReceive(new AsyncCallback(OnReceive), ((RouterState)(res.AsyncState)));
            } catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
        }

        private void Run()
        {
            // initializes listeners.
            for (int i = 0; i < servers.Length; i++)
            {
                UdpClient listener = new UdpClient(servers[i].Port);

                Console.WriteLine($"listening for messages on port[{servers[i].Port}] and ip[{servers[i].Ip}]");
                listener.BeginReceive(new AsyncCallback(OnReceive), (i + 1, listener));
            }

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
                            int[] updateRow2 = { 7, 0, 2, int.MaxValue };
                            int[] updateRow3 = { 4, 2, 0, 1 };
                            int[] updateRow4 = { 3, int.MaxValue, 0, 0 };

                            d1.ReadTopFile();
                            d1.UpdateRow(2, updateRow2);
                            d1.UpdateRow(3, updateRow3);
                            d1.UpdateRow(4, updateRow4);
                            d1.DVectorAlg();
                            d1.Display();
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
                    case "exit":
                        {
                            Console.WriteLine("All connections closing, good bye...");
                            /* end listeners
                            foreach ((int id, string address, int port) in peerManager.GetPeerList())
                            {
                                peerManager.TerminatePeer(id);
                            }
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

        public void BellmanFord()
        {
            Console.WriteLine("\nDistance Vector: ");
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
