using System;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;

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

        internal bool log = false;

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

            if (log)
                Console.WriteLine("Setup Server with Port: {0}", port);

            serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            serverSocket.Listen(globalVar.SERVER_MAX_PENDING_CONNECTIONS);
            serverSocket.BeginAccept(new AsyncCallback(acceptCallback), null);
        }

        public void enableConsole(bool console = true)
        {
            this.log = console;
        }

        private void acceptCallback(IAsyncResult ar)
        {
            Socket socket = serverSocket.EndAccept(ar);

            if (log)
                Console.WriteLine("Connection from {0} recieved.", socket.RemoteEndPoint.ToString());

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
                if (log)
                    Console.WriteLine("Max Number of Clients connected is reached. IP: {0} got declined.", socket.RemoteEndPoint.ToString());
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
                if (log)
                    Console.WriteLine("Couldn't find a matching Function to execute.");
            }
        }
        #endregion
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

            if (server.log)
                Console.WriteLine("Client: {0} is set up.", id);
        }

        private void recieveCallback(IAsyncResult ar)
        {
            if (server.log)
                Console.WriteLine("Data from Client: {0} recieved.", id);

            Socket socket = (Socket)ar.AsyncState;

            try
            {
                //length of recieved buffer
                int recievedLength = socket.EndReceive(ar);

                //zero bytes are sent
                if (recievedLength <= 0)
                {
                    if (server.log)
                        Console.WriteLine("RecievedLength <= 0, Client-ID: {0}", id);

                    closeClient();
                }
                else
                {
                    byte[] recievedData = new byte[recievedLength];
                    Array.Copy(buffer, recievedData, recievedLength);

                    if (server.log)
                        Console.WriteLine("Recieved Byte-Array Length: {1}, Client-ID: {0}", id, recievedLength);

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

            if (server.log)
                Console.WriteLine("Connection from {0} has been terminated. Client-ID: {1}", ip, id);

            //Client Disconnected
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
                if (server.log)
                    Console.WriteLine("Couldn't handle Data with Length: {0}", data.Length);
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
                if(server.log)
                    Console.WriteLine("Error sending Message to the Client: " + this.ip);
            }
        }
        #endregion
    }
}
