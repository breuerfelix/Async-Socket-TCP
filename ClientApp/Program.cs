using System;
using Bindings;

namespace ClientApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ClientApp started.");

            clientTCP client = new clientTCP();
            client.setupClient();
            client.connect();

            client.handleFunctions.Add("string", handleStringMessage);

            dataPackage pack = new dataPackage();
            pack.write("string");
            pack.write("hallo das ist meine message - CLIENT");

            client.sendData(pack.toArray());
            pack.Dispose();

            Console.ReadLine();

        }

        public static void handleStringMessage(byte[] data)
        {
            dataPackage pack = new dataPackage();

            pack.write(data);

            Console.WriteLine(pack.readString());
            Console.WriteLine(pack.readString());
        }
    }

}
