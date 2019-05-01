using System;
using System.Text.RegularExpressions;

namespace RouterApp
{
    class Program
    {
        static void Main(string[] args)
        {

            //int id = args.Length != 0 ? Int32.Parse(args[0]) : 1;

            //Console.WriteLine(id);

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
                                "entries."
                                );
                            break;
                        }
                    case var val when new Regex(@"^server\s+-t\s+(.+)\s+-i\s+(\d+)$").IsMatch(val):
                        {
                            var m = new Regex(@"^server\s+-t\s+(.+)\s+-i\s+(.+)$").Match(line);
                            string file = m.Groups[1].Captures[0].Value;
                            int interval = Int32.Parse(m.Groups[2].Captures[0].Value);

                            Router me = new Router(file, interval);
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

        }
    }
}
