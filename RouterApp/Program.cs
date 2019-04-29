using System;

namespace RouterApp
{
    class Program
    {
        static void Main(string[] args)
        {

            int id = args.Length != 0 ? Int32.Parse(args[0]) : 1;

            Console.WriteLine(id);

            Router me = new Router(id);
        }
    }
}
