using System;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Collections.Generic;

using ClientServer;

namespace ClientApp
{
    /*  
        HOW TO USE

        //Setup Client
        clientTCP client = new clientTCP(); 
        client.setupClient();
        client.connect();

        //connect the Delegate Function
        client.handleFunctions.Add("message", handleStringMessage);
        make a function called handleStringMessage(byte[] data);
            
        //Send Data to the Server
        dataPackage pack = new dataPackage();
        pack.write("message");
        pack.write("Hello, this is a Message. - CLIENT");
        client.sendData(pack.toArray());
        pack.Dispose();
    */

    public class clientTCP
    {
        private Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private byte[] asynchbuffer = new byte[globalVar.BUFFER_BYTE];

        private string ip;
        private int port;
        private bool setup = false;
        private bool log = false;
        private bool connected = false;

        #region Setup
        public void setupClient(string ip = "127.0.0.1", int port = 0)
        {
            if (port == 0)
                port = globalVar.SERVER_PORT;

            this.ip = ip;
            this.port = port;
            setup = true;

            if (log)
                Console.WriteLine($"Client set up to communicate with Server {ip} with Port {port}");
        }

        public void enableConsole(bool console = true)
        {
            this.log = console;
        }

        public void connect()
        {
            if (setup)
            {
                if (log)
                    Console.WriteLine("Connection to server...");

                try
                {
                    clientSocket.BeginConnect(ip, port, new AsyncCallback(connectCallback), clientSocket);
                    connected = true;
                }
                catch
                {
                    connected = false;

                    if (log)
                        Console.WriteLine("Failed to connect to Server.");
                }
            }
            else
            {
                if (log)
                    Console.WriteLine("Client isn't set up yet. Use setupClient().");
            }
        }

        private void connectCallback(IAsyncResult ar)
        {
            if (log)
                Console.WriteLine("Connected.");

            try
            {
                clientSocket.EndConnect(ar);
            }
            catch
            {
                connected = false;
            }

            while (connected)
            {
                try
                {
                    connected = onRecieve();
                }
                catch
                {
                    connected = false;

                    if (log)
                        Console.WriteLine("Error recieving Messages.");
                }
            }

            if (log)
                Console.WriteLine("Disconnected. Use connect() to reconnect.");
        }

        private bool onRecieve()
        {
            bool successful = true;

            int sizeInfoSize = globalVar.DATA_SIZE_INFO_SIZE;
            byte[] _sizeInfo = new byte[sizeInfoSize];
            byte[] _recieveBuffer = new byte[globalVar.BUFFER_BYTE];

            int totalread = 0;
            int currentread = 0;

            try
            {
                currentread = totalread = clientSocket.Receive(_sizeInfo);
                if (totalread <= 0)
                {
                    if (log)
                        Console.WriteLine("totalread is <= 0");

                    successful = false;
                }
                else
                {
                    while (totalread < _sizeInfo.Length & currentread > 0)
                    {
                        currentread = clientSocket.Receive(_sizeInfo, totalread, _sizeInfo.Length - totalread, SocketFlags.None);
                        totalread += currentread;
                    }

                    int messagesize = 0;

                    for (int i = 0; i < sizeInfoSize; i++)
                    {
                        messagesize |= (_sizeInfo[i] << (i * 8));
                    }

                    byte[] data = new byte[messagesize];

                    totalread = 0;
                    currentread = totalread = clientSocket.Receive(data, totalread, data.Length - totalread, SocketFlags.None);

                    while (totalread < messagesize & currentread > 0)
                    {
                        currentread = clientSocket.Receive(data, totalread, data.Length - totalread, SocketFlags.None);
                        totalread += currentread;
                    }

                    if (log)
                        Console.WriteLine("Recieved Byte-Array Length: {0}", messagesize);

                    //handle recieved data
                    handleRecievedData(data);

                    successful = true;
                }
            }
            catch
            {
                if (log)
                    Console.WriteLine("You are not connected to the server!");

                successful = false;
            }

            return successful;
        }
        #endregion

        #region Send Data
        public void sendData(byte[] data)
        {
            if (connected)
            {
                try
                {
                    clientSocket.Send(data);
                }
                catch
                {
                    connected = false;
                }
            }
            else
            {
                if(log)
                    Console.WriteLine("Can't send Data. You are not connected to the Server.");
            }
        }
        #endregion

        #region Handle Data
        public delegate void handleServerData(byte[] data);
        public Dictionary<string, handleServerData> handleFunctions = new Dictionary<string, handleServerData>();

        private void handleData(byte[] data)
        {
            dataPackage pack = new dataPackage();
            pack.write(data);
            string enumString = pack.readString();
            pack.Dispose();

            handleServerData function;

            if (handleFunctions.TryGetValue(enumString, out function))
            {
                function.Invoke(data);
            }
            else
            {
                if (log)
                    Console.WriteLine("Couldn't find a matching Function to execute.");
            }
        }
        private void handleRecievedData(byte[] data)
        {
            try
            {
                handleData(data);
            }
            catch
            {
                //Couldn't handle Data
                if (log)
                    Console.WriteLine("couldnt handle data");
            }
        }
        #endregion
    }
}
