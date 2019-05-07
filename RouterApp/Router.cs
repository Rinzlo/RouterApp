using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Timers;
using System.Linq;

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

        public string Info
        {
            get
            {
                return $"\nRouter Info:" +
                        $"\nID: {serverId}" +
                        $"\nIP: {servers[serverId - 1].Ip}" +
                        $"\nPORT: {servers[serverId - 1].Port}";
            }
        }

        private UdpClient msgListener;

        private int toInterval;
        private int[] missedIntervals;
        private int MAX_MISSED_INTERVALS = 3;

        public int ServerCount { get { return servers.Length; } }

        RouterState[] servers;

        int[,] table;

        int[] parents;

        int[] dist;

        int numEdges;
        int serverId;
        int packets = 0;       // counter for packets received  


        public Router(string file, int interval)
        {
            toInterval = interval;
            ReadTopFile(file);
            DisplayTopFile();
            DisplayTable();
            Console.WriteLine(Info);

            BellmanFord();
            DisplayDist();

            //TODO: start msgListener with file read port
            Run();
        }

        #region UDP
        public void OnReceive(IAsyncResult res)
        {
            (int id, UdpClient client) = ((int, UdpClient))res.AsyncState;
            msgListener = client;
            IPEndPoint endPoint = servers[id].EndPoint;

            //TODO: check if this is a timeout update.
            // if so, check if it is a new connection.

            try
            {
                byte[] bytes = msgListener.EndReceive(res, ref endPoint);
                string receiveString = Encoding.ASCII.GetString(bytes);
                packets++;
                // row: [id, col0, col1, ..., coln]


                // quick fix
                // the ∞ in the receive string become "?" when its received
                receiveString = receiveString.Replace("?", int.MaxValue.ToString());

                Console.WriteLine("Received string is " + receiveString);
                string[] receivedRow = receiveString.Split(' '); ;
                // dont cut off the first element of the array



                //Array.Copy(receivedRow, 1, receivedRow, 0, receiveString.Length-1);

                Console.WriteLine(string.Join(",", receivedRow));

                // ill do this more elegantly later, for now lets just test this
                // if we get a disable command do it on our table
                switch(receivedRow[0])
                {
                    case "disable":
                        int source = int.Parse(receivedRow[1]);
                        int destination = int.Parse(receivedRow[2]);
                        Console.WriteLine($"Received command to disable server {source + 1} {destination + 1}");

                        disableEdge(source, destination);
                        break;
                    case "UpdateRow":
                        String[] myArray = new String[100];

                        //lock (receivedRow.SyncRoot)
                        // {
                        //    Array.Copy(receivedRow, myArray, receiveString.Length + 100);

                        //}

                        //    Array.Copy(receivedRow, 1, receivedRow, 0, receiveString.Length);

                        Console.WriteLine("Update row receved this");
                        Console.WriteLine(string.Join(",", receivedRow));


                        UpdateRow(int.Parse(receivedRow[1]), receivedRow);
                        break;
                    case "globalUpdate":
                        // perform the request row update 
                        GlobalUpdate(int.Parse(receivedRow[1]), int.Parse(receivedRow[2]), int.Parse(receivedRow[3]));
                        // probably redo the bellman ford
                        BellmanFord();
                        break;
                    case "crash":
                        // if we receive crash command sever   EX we get "crash 1" then handle crashing 1
                        crash(int.Parse(receivedRow[1]));
                        break;
                    case "checkin":
                        missedIntervals[int.Parse(receivedRow[1])-1] = 0;
                        break;
                }


                //UpdateRow(int.Parse(receiveString[0]), receivedRow);
                DisplayTable();

                Console.WriteLine($"Received: {receiveString}");

                msgListener.BeginReceive(new AsyncCallback(OnReceive), res.AsyncState);
            }
            catch (Exception e)
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

        public void SetupMessageListener()
        {
            // initializes msgListeners.
            RouterState myServer = servers[serverId - 1];
            msgListener = new UdpClient(myServer.Port);

            Console.WriteLine($"listening for messages on port[{myServer.Port}] and ip[{myServer.Ip}]");
            msgListener.BeginReceive(new AsyncCallback(OnReceive), (serverId, msgListener));
        }
        #endregion

        private void IntervalBroadcast(Object source, ElapsedEventArgs e)
        {
            for (int i = 0; i < table.GetLength(1); i++)
            {
                //Console.WriteLine($"parents[{i+1}]: {parents[i]+1}, serverId: {serverId}");
                // Only check neihbors who are not inf dist (disconnected)
                if (serverId - 1 != i && parents[i] == serverId - 1 && table[serverId - 1, i] != int.MaxValue)
                {
                    // send a checkin broadcast
                    Send(servers[i], $"checkin {serverId}");
                    if (missedIntervals[i] >= MAX_MISSED_INTERVALS)
                    {
                        //Disconnect from server i.
                        Console.WriteLine($"disconnecting router {i + 1}");
                        UpdateEdge(serverId, i + 1, int.MaxValue);
                    }
                    else
                    {
                        // Console.WriteLine($"Router-{i+1}: {missedIntervals[i]}");
                        missedIntervals[i]++;
                    }
                }
                else
                {
                    // either our router, or inf distance
                    /*
                    Console.WriteLine($"serverId: {serverId}\n" +
                        $"i: {i+1}\n" +
                        $"table[{serverId},{i+1}]: {table[serverId-1,i]}\n");
                    /**/
                }
            }
            //Console.WriteLine("Timer...");
        }

        private void Run()
        {
            SetupMessageListener();

            /**/
            Timer timer = new Timer(toInterval * 1000);
            timer.AutoReset = true;
            timer.Elapsed += new ElapsedEventHandler(IntervalBroadcast);
            timer.Start();
            /**/
            string line = "";

            Console.WriteLine("Type [help] for a list of commands...\n");

            while ((line = Console.ReadLine()) != null)
            {
                Console.WriteLine();

                switch (line)
                {
                    case "help":
                        {
                            Console.WriteLine("Commands:\n" +
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

                            Send(servers[id - 1], msg);
                            Console.WriteLine("Message sent to peer with id " + id.ToString());

                            break;
                        }
                    case var val when new Regex(@"^update\s+(\d{1})\s+(\d+)\s+(\d+)$").IsMatch(val):
                        {
                            var m = new Regex(@"^update\s+(\d{1})\s+(\d+)\s+(\d+)$").Match(line);
                            int id1 = Int32.Parse(m.Groups[1].Captures[0].Value);
                            int id2 = Int32.Parse(m.Groups[2].Captures[0].Value);
                            //TODO: allow for inf.
                            int cost = Int32.Parse(m.Groups[3].Captures[0].Value);

                            UpdateEdge(id1, id2, cost);
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
                            }
                            else
                            {
                                linkCost = int.Parse(tmpLinkCost);
                            }

                            Console.WriteLine($"values pulled {servEdge} {endEdge}");
                            Console.WriteLine($"link cost is {linkCost}");

                            UpdateEdge(servEdge, endEdge, linkCost);
                            break;
                        }

                    case "step":
                        {


                            string[] updateArr = dist.Select(x => x.ToString()).ToArray();

                            step(updateArr);
                            //step(test2);
                            break;
                        }
                    case "crash":
                        {

                            Console.WriteLine("CRASH occured: links set to infinity");
                            crash(serverId);
                            SendCrash();
                            break;
                        }
                    case "display":
                        {

                            DisplayDist();
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

                                table[serverId - 1, disabledServer - 1] = int.MaxValue;
                                table[disabledServer - 1, serverId - 1] = int.MaxValue;
                                BellmanFord();

                                for (int i = 1; i <= ServerCount; i++)
                                {

                                    if (i == serverId)
                                    {
                                        // dont send a disable to yourself again
                                    }
                                    else
                                    {
                                        // send everyone else the command to disable the server
                                        Send(servers[i - 1], $"disable {serverId} {disabledServer}");
                                    }

                                }

                            }

                            BellmanFord();
                            DisplayDist();
                            break;
                        }
                    case "exit":
                        {
                            Console.WriteLine("All connections closing, good bye...");
                            /* end msgListener */
                            /**/
                            Console.WriteLine("Terminating connections");

                            msgListener.Close();
                            timer.Stop();
                            return;
                        }
                    case "displaytable":
                        {
                            DisplayTable();
                            break;
                        }
                    default:
                        {
                            Console.WriteLine("Error: Invalid Command, type [help] for a list of commands...");
                            break;
                        }
                }
                Console.WriteLine();
            }
            msgListener.Close();
            timer.Stop();
        }

        #region fileIO
        public void Setup(int servCount)
        {
            table = new int[servCount, servCount];
            dist = new int[servCount];
            parents = new int[servCount];
            missedIntervals = new int[servCount];

            for (int i = 0; i < table.GetLength(0); i++)
            {

                dist[i] = int.MaxValue;

                for (int j = 0; j < table.GetLength(1); j++)
                {
                    // set all entries to effectively infinity 
                    table[i, j] = int.MaxValue;
                }

            }
            // need to move this for algorithm to work for each server 
            //dist[1] = 0;
        }

        // TODO: refactor file string reading code.
        // reads the Topology file, and sets up everything
        public void ReadTopFile(string file)
        {
            try
            {
                StreamReader sr = new StreamReader(file);
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
                dist[serverId - 1] = 0;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Topology file '{file}' does not exist\nStack Trace: {e}");
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
            Console.WriteLine();
            //Console.WriteLine("{0,15} - {1, 17}", "Table:", "something");
            Console.WriteLine($"{"Table:",15} - {"something",20}");
            Console.WriteLine("__________________________________________________________");
            Console.WriteLine("   |                                                      |");
            for (int i = 0; i < table.GetLength(0); i++)
            {
                Console.Write($"{i+1}  |  ");
                for (int j = 0; j < table.GetLength(1); j++)
                {
                    //TODO: output grid format
                    if (table[i, j] < int.MaxValue)
                    {
                        if(j != 0)  Console.Write("\t\t");
                        Console.Write(table[i, j]);
                        if (j == table.GetLength(1)-1) Console.Write("\t");
                    }
                    else
                    {
                        if(j != 0)  Console.Write("\t");
                        Console.Write("Infinity");
                    }
                }
                Console.WriteLine("  |");
            }

            Console.WriteLine("___|______________________________________________________|");
        }


        public void DisplayDist()
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
            for (int j = 0; j < table.GetLength(1); j++)
            {

                table[rowNum, j] = int.Parse(newRowArr[j + 2]);
            }
            // run bellman ford here 
            BellmanFord();
        }

        // update 1 2 7   where 1 is serverID, 2 is destId, and 7 is link cost 
        public void UpdateEdge(int sourceId, int destId, int linkCost)
        {
            if ((sourceId < 1 || sourceId > ServerCount) || (destId < 1 || destId > ServerCount))
            {
                Console.WriteLine("Inputed server or edge does not exist!");
            }
            else
            {

                if (sourceId == serverId)
                {
                    table[sourceId - 1, destId - 1] = linkCost;
                    table[destId - 1, sourceId - 1] = linkCost;

                    // send command to all other servers to update this edge 
                    for (int i = 0; i < ServerCount; i++)
                    {
                        // Only update neighbors
                        if(serverId != i+1 && parents[i] == serverId - 1)
                            Send(servers[i], $"globalUpdate {sourceId} {destId} {linkCost}");
                    }

                    Console.WriteLine($"Edge {sourceId} {destId} was set to {linkCost}");

                    DisplayTable();
                }
                else
                {
                    Console.WriteLine("You tried to update a value that is not involved");
                }
            }

            BellmanFord();
        }

        public void GlobalUpdate(int sourceId, int destId, int linkCost)
        {

            table[sourceId - 1, destId - 1] = linkCost;
            table[destId - 1, sourceId - 1] = linkCost;

            // We should do BellmanFord again
            // not yet until I resolve the other issue    ------- i think i solved this
            BellmanFord();
        }

        // when we receive a disable message from another server we run this to disable those same edges
        // EX server1 calls disable 2
        //    sends message to server2 which results in this call disableEdge(source, destination)
        // important distinction this method allows us to change an edge of another server
        public void disableEdge(int source, int destination)
        {
            table[source - 1, destination - 1] = int.MaxValue;
            table[destination - 1, source - 1] = int.MaxValue;

            // rerun bellman ford
            BellmanFord();
            // send if changed?

        }

        // send my routing update to everyone
        // this should send our DV aka the dist[]
        // giving an array as a parameter for testing.  I will change to dist once I make the other fixes
        public void step(String[] stepArr)
        {

            String stepMsg = $"UpdateRow {serverId} " + string.Join(" ", stepArr);
            Console.WriteLine("Sending step " + stepMsg);

            for (int i = 1; i <= ServerCount; i++)
            {

                if (serverId == i)
                {

                }
                else
                {

                    Send(servers[i - 1], stepMsg);
                }
            }


        }

        //crashes all connections to it and its connections to eerything else
        public void crash(int crashId)
        {
            for (int i = 0; i < table.GetLength(0); i++)
            {
                table[i, crashId - 1] = int.MaxValue;
                table[crashId - 1, i] = int.MaxValue;

            }
            Console.WriteLine("crash: SUCCESS");
            BellmanFord();
        }

        // tell all servers to handle this crash
        public void SendCrash()
        {
            for (int i = 1; i <= ServerCount; i++)
            {
                if (serverId == i)
                {

                }
                else
                {
                    Send(servers[i - 1], $"crash {serverId}");
                }
            }
        }

        public void ResetDist()
        {
            for (int i = 0; i < dist.GetLength(0); i++)
            {
                if (i == serverId - 1)
                {
                    dist[i] = 0;
                }
                else
                {
                    dist[i] = int.MaxValue;
                }

            }

        }

        public void BellmanFord()
        {
            //TODO: cache our server's row
            // dont know how to use this for how im doing the fix
            /*
            int[] rowBefore = new int[table.Length];
            Buffer.BlockCopy(table, serverId, rowBefore, 0, 1);
            */
            int[] prevDist = dist;
            Console.WriteLine("here is the copy");
            Console.WriteLine(String.Join(",", prevDist));


            // i think we need to reset the distance table in order for this to work
            ResetDist();
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

            if (!prevDist.Equals(dist))
            {
                Console.WriteLine("The dist was changed send the update to neighbors");
            }



            // the result of running the algorithm
            Console.Write("Distance ");
            Console.WriteLine("[{0}]", string.Join(", ", dist));
            //Console.Write("Parent ");
            //Console.WriteLine("[{0}]", string.Join(", ", parent));



            int[] rowAfter = new int[table.Length];
            Buffer.BlockCopy(table, serverId, rowAfter, 0, 1);
            // If our new row is different from the cached row, broadcast an update.
            /*
            if (Enumerable.SequenceEqual(rowAfter, rowBefore))
            {
                //TODO: broadcast our new row.
                Console.WriteLine("sending Row Update");
            }
            */
        }
    }
}
