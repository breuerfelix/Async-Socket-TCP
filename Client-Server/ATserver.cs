using System;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using System.Threading;

using ClientServer;

namespace AsyncTCPserver
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
    public delegate void clientHandler(int clientID);

    public delegate void clientByteHandler(int clientID, byte[] data);
    public delegate void stringHandler(string message);

    public class ATserver
    {
        public static int MAX_PENDING_CONNECTIONS = 20;
        public static int MAX_CLIENTS = 1000;

        public static int BUFFER_SIZE = 1024;
        public static int PACKAGE_LENGTH_SIZE = 4;

        private Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        private List<client> clients = new List<client>();

        //public events
        public event clientByteHandler recievingData;
        public event clientHandler clientConnecting;
        public event clientHandler clientDisconnecting;

        //log events
        public event stringHandler consoleLogged;

        #region Connect
        public void start(int port = 5000)
        {
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            serverSocket.Listen(ATserver.MAX_PENDING_CONNECTIONS);
            serverSocket.BeginAccept(new AsyncCallback(acceptCallback), serverSocket);

            log($"Server listening on Port: {port}");
        }

        private void acceptCallback(IAsyncResult ar)
        {
            try
            {
                Socket serverS = ar.AsyncState as Socket;

                Socket clientSocket = serverS.EndAccept(ar);
                serverS.BeginAccept(new AsyncCallback(acceptCallback), serverS);

                log($"Connection from {clientSocket.RemoteEndPoint.ToString()} recieved.");

                if (clients.Count < ATserver.MAX_CLIENTS)
                {
                    client c = new client(clientSocket);
                    clients.Add(c);

                    c.clientDisconnecting += clientDisconnected;
                    c.recievingData += recievedData;
                    c.consoleLogging += log;

                    //start the client
                    c.startClient();
                    clientConnecting?.Invoke(c.id);
                }
                else
                {
                    clientSocket.Close();
                    log($"Max Number of Clients connected is reached. IP: {clientSocket.RemoteEndPoint.ToString()} got declined.");
                }
            }
            catch
            {
                log("Error accepting connection!");
            }
        }

        private void clientDisconnected(int clientID)
        {
            client client = clients.Find(x => x.id == clientID);

            if (client != null)
            {
                clientDisconnecting?.Invoke(clientID);

                clients.Remove(client);

                client.clientDisconnecting -= clientDisconnected;
                client.recievingData -= recievedData;
                client.consoleLogging -= log;
            }
        }

        private void recievedData(int clientID, byte[] data)
        {
            recievingData?.Invoke(clientID, data);
        }
        #endregion

        #region Send
        public void sendDataTo(int id, byte[] data)
        {
            client c = clients.Find(x => x.id == id);

            if (c != null)
            {
                c.sendData(data);
            }
            else
            {
                log($"Couldn't find client with ID: {id}");
            }
        }
        #endregion

        #region HandleData
        public Dictionary<string, clientByteHandler> handleFunctions = new Dictionary<string, clientByteHandler>();

        internal void handleData(int clientID, byte[] data)
        {
            dataPackage pack = new dataPackage();
            pack.write(data);
            string enumString = pack.readString();
            pack.Dispose();

            clientByteHandler function;

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

    internal class client
    {
        public int id;
        private static List<int> idList = new List<int>();

        private Socket socket = null;

        public event clientByteHandler recievingData;
        public event clientHandler clientDisconnecting;
        public event stringHandler consoleLogging;

        public client(Socket s)
        {
            id = 0;

            while (idList.Contains(id))
            {
                id++;
            }

            this.socket = s;
        }

        #region Setup
        public void startClient()
        {
            packageState package = new packageState(this.socket);

            socket.BeginReceive(package.sizeBuffer, 0, package.sizeBuffer.Length, SocketFlags.None, new AsyncCallback(recieveCallback), package);

            log($"Client: {id} is set up.");
        }

        private void recieveCallback(IAsyncResult ar)
        {
            log($"Data from Client: {id} recieved.");

            try
            {
                packageState package = ar.AsyncState as packageState;
                Socket clientS = package.socket;

                int bytesRead = clientS.EndReceive(ar);

                if (bytesRead > 0)
                {
                    int size = ATserver.BUFFER_SIZE;
                    if (package.readOffset == -1)
                    {
                        size = BitConverter.ToInt32(package.sizeBuffer, 0);
                        package.readBuffer = new byte[size];
                        package.readOffset = 0;

                    }
                    else
                    {
                        package.readOffset += bytesRead;

                        if (package.readOffset == package.readBuffer.Length)
                        {
                            //clone array so the package can be disposed
                            byte[] temp = package.readBuffer.Clone() as byte[];

                            //invoke the event
                            recievingData?.Invoke(this.id, temp);

                            //free memory
                            package.Dispose();
                            package = new packageState(clientS);
                        }
                    }

                    if (package.readBuffer == null)
                    {
                        package.socket.BeginReceive(package.sizeBuffer, 0, package.sizeBuffer.Length, SocketFlags.None, new AsyncCallback(recieveCallback), package);
                    }
                    else
                    {
                        int readsize = (ATserver.BUFFER_SIZE > size) ? size : ATserver.BUFFER_SIZE;
                        package.socket.BeginReceive(package.readBuffer, package.readOffset, readsize, SocketFlags.None, new AsyncCallback(recieveCallback), package);
                    }
                }
                else
                {
                    log("Error recieving Message! ReadBytes < 0.");
                    closeClient();
                }
            }
            catch
            {
                log("Error recieving Message!");
                closeClient();
            }
        }

        private void closeClient()
        {
            log($"Connection from {socket.RemoteEndPoint.ToString()} has been terminated. Client-ID: {id}");

            socket.Close();

            //Client Disconnected
            clientDisconnecting(this.id);
        }
        #endregion

        #region Send / Recieve Data

        public void sendData(byte[] data)
        {
            try
            {
                //send sizeinfo
                byte[] sizeInfo = BitConverter.GetBytes(data.Length);
                this.socket.BeginSend(sizeInfo, 0, sizeInfo.Length, SocketFlags.None, new AsyncCallback(sendCallback), this.socket);

                //send data
                this.socket.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(sendCallback), this.socket);
            }
            catch
            {
                log("Error sending Message to the Client: " + this.id);
                closeClient();
            }
        }

        private void sendCallback(IAsyncResult ar)
        {
            try
            {
                Socket clientS = ar.AsyncState as Socket;
                int sizeSend = clientS.EndSend(ar);

                log($"Sent {sizeSend} Bytes to the Server.");
            }
            catch
            {
                log("Failed sending Message!");
                closeClient();
            }
        }
        #endregion

        private void log(string message)
        {
            consoleLogging?.Invoke(message);
        }
    }

    internal class packageState : IDisposable
    {
        public Socket socket = null;
        public byte[] sizeBuffer = new byte[ATserver.PACKAGE_LENGTH_SIZE];
        public int readOffset = -1;
        public byte[] readBuffer = null;

        public packageState(Socket s)
        {
            this.socket = s;
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    socket = null;
                    sizeBuffer = null;
                    readBuffer = null;
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
