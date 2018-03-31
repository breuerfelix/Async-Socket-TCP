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

        private bool _connected;
        private bool connected
        {
            get
            {
                lock (Lock)
                {
                    return _connected;
                }
            }
            set
            {
                lock(Lock){
                    _connected = value;
                }
            }
        }

        //log events
        public delegate void consoleLog(string message);
        public event consoleLog consoleLogged;

        private static Object Lock = new Object();

        #region Setup
        public void setupClient(string ip = "127.0.0.1", int port = 0)
        {
            if (port == 0)
                port = globalVar.SERVER_PORT;

            this.ip = ip;
            this.port = port;
            setup = true;

            log($"Client set up to communicate with Server {ip} with Port {port}");
        }

        public void connect()
        {
            if (setup)
            {
                log("Connection to server...");

                try
                {
                    clientSocket.BeginConnect(ip, port, new AsyncCallback(connectCallback), clientSocket);
                }
                catch
                {
                    connected = false;

                    log("Failed to connect to Server.");
                }
            }
            else
            {
                log("Client isn't set up yet. Use setupClient().");
            }
        }

        private void connectCallback(IAsyncResult ar)
        {
            try
            {
                clientSocket.EndConnect(ar);
                log("Connected.");
                connected = true;
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

                    log("Error recieving Messages.");
                }
            }

            log("Disconnected. Use connect() to reconnect.");
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
                    log("totalread is <= 0");

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

                    log($"Recieved Byte-Array Length: {messagesize}");

                    //handle recieved data
                    handleRecievedData(data);

                    successful = true;
                }
            }
            catch
            {
                log("You are not connected to the server!");

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
                log("Can't send Data. You are not connected to the Server.");
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
                log("Couldn't find a matching Function to execute.");
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
                log("couldnt handle data");
            }
        }
        #endregion

        internal void log(string message)
        {
            consoleLogged?.Invoke(message);
        }
    }
}
