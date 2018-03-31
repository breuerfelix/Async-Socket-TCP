using System;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;

using ClientServer;

namespace ServerApp
{
    /*  
        HOW TO USE

        //Setup Server
        serverTCP server = new serverTCP(); 
        server.setupServer();

        //connect the Delegate Function
        server.handleFunctions.Add("message", handleStringMessage);
        make a function called handleStringMessage(int clientID, byte[] data);

        //Send Data to the Client
        dataPackage pack = new dataPackage();
        pack.write("message");
        pack.write("Hello, this is a Message. - CLIENT");
        server.sendData(clientID, pack.toArray());
        pack.Dispose();
    */
    public delegate void handleClientData(int clientID, byte[] data);

    public class serverTCP
    {
        private Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private byte[] buffer = new byte[globalVar.BUFFER_BYTE];

        private client[] clients = new client[globalVar.MAX_CLIENTS];

        public delegate void clientFunction(int clientID);
        public event clientFunction clientConnected;
        public event clientFunction clientDisconnected;

        //log events
        public delegate void consoleLog(string message);
        public event consoleLog consoleLogged;

        #region Setup
        public void setupServer(int port = 0)
        {
            for (int i = 0; i < clients.Length; i++)
            {
                clients[i] = new client
                {
                    id = i,
                    server = this
                };
            }

            if (port == 0)
                port = globalVar.SERVER_PORT;

            log($"Setup Server with Port: {port}");

            serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            serverSocket.Listen(globalVar.SERVER_MAX_PENDING_CONNECTIONS);
            serverSocket.BeginAccept(new AsyncCallback(acceptCallback), null);
        }

        private void acceptCallback(IAsyncResult ar)
        {
            Socket socket = serverSocket.EndAccept(ar);

            log($"Connection from {socket.RemoteEndPoint.ToString()} recieved.");

            serverSocket.BeginAccept(new AsyncCallback(acceptCallback), null);

            bool added = false;

            for (int i = 0; i < clients.Length; i++)
            {
                if (!clients[i].used)
                {
                    client c = clients[i];
                    c.socket = socket;
                    c.ip = socket.RemoteEndPoint.ToString();

                    //start the client
                    c.startClient();
                    clientConnected?.Invoke(c.id);

                    added = true;
                    break;
                }
            }

            if (!added)
            {
                log($"Max Number of Clients connected is reached. IP: {socket.RemoteEndPoint.ToString()} got declined.");
            }
        }

        internal void disconnectedClient(int clientID)
        {
            clientDisconnected?.Invoke(clientID);
        }
        #endregion

        #region Send Data
        public void sendDataTo(int id, byte[] data)
        {
            client c = clients[id];

            c.sendData(data);
        }
        #endregion

        #region HandleData
        public Dictionary<string, handleClientData> handleFunctions = new Dictionary<string, handleClientData>();

        internal void handleData(int clientID, byte[] data)
        {
            dataPackage pack = new dataPackage();
            pack.write(data);
            string enumString = pack.readString();
            pack.Dispose();

            handleClientData function;

            if (handleFunctions.TryGetValue(enumString, out function))
            {
                function.Invoke(clientID, data);
            }
            else
            {
                log("Couldn't find a matching Function to execute.");
            }
        }
        #endregion

        internal void log(string message)
        {
            consoleLogged?.Invoke(message);
        }
    }

    class client
    {
        internal int id;
        internal string ip;
        internal Socket socket;
        internal bool used = false;
        internal serverTCP server;
        private byte[] buffer = new byte[globalVar.BUFFER_BYTE];

        #region Setup
        public void startClient()
        {
            used = true;
            socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(recieveCallback), socket);

            server.log($"Client: {id} is set up.");
        }

        private void recieveCallback(IAsyncResult ar)
        {
            server.log($"Data from Client: {id} recieved.");

            Socket socket = (Socket)ar.AsyncState;

            try
            {
                //length of recieved buffer
                int recievedLength = socket.EndReceive(ar);

                //zero bytes are sent
                if (recievedLength <= 0)
                {
                    server.log($"RecievedLength <= 0, Client-ID: {id}");

                    closeClient();
                }
                else
                {
                    byte[] recievedData = new byte[recievedLength];
                    Array.Copy(buffer, recievedData, recievedLength);

                    server.log($"Recieved Byte-Array Length: {id}, Client-ID: {recievedLength}");

                    //handle recieved Data
                    handleRecievedData(recievedData);

                    socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(recieveCallback), socket);
                }
            }
            catch
            {
                //Client Disconnects
                closeClient();
            }
        }

        private void closeClient()
        {
            used = false;

            server.log($"Connection from {ip} has been terminated. Client-ID: {id}");

            //Client Disconnected
            server.disconnectedClient(this.id);
            socket.Close();
        }
        #endregion

        #region Send / Recieve Data
        private void handleRecievedData(byte[] data)
        {
            try
            {
                server.handleData(this.id, data);
            }
            catch
            {
                server.log($"Couldn't handle Data with Length: {data.Length}");
            }
        }

        public void sendData(byte[] data)
        {
            int size = globalVar.DATA_SIZE_INFO_SIZE;

            byte[] sizeInfo = new byte[size];
            for (int i = 0; i < size; i++)
            {
                sizeInfo[i] = (byte)(data.Length >> (i * 8));
            }

            try
            {
                socket.Send(sizeInfo);
                socket.Send(data);
            }
            catch
            {
                server.log("Error sending Message to the Client: " + this.ip);
            }
        }
        #endregion
    }
}
