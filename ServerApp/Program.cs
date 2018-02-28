using System;
using Bindings;

namespace ServerApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ServerApp started.");
            serverTCP server = new serverTCP();
            server.setupServer();

            server.handleFunctions.Add("string", handleStringMessage);
            Console.ReadLine();

            dataPackage pack = new dataPackage();
            pack.write("string");
            pack.write("hallo das ist meine message - SERVER");

            server.sendDataTo(0, pack.toArray());
            pack.Dispose();


            Console.ReadLine();
        }

        public static void handleStringMessage(int client, byte[] data)
        {
            dataPackage pack = new dataPackage();

            pack.write(data);

            Console.WriteLine(pack.readString());
            Console.WriteLine(pack.readString());
        }
    }
}
