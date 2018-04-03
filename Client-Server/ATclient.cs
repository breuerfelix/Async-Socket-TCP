using System;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Collections.Generic;

using ClientServer;

namespace AsyncTCPclient
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

    public class ATclient
    {
        public static int BUFFER_SIZE = 1024;
        public static int PACKAGE_LENGTH_SIZE = 4;

        public delegate void byteHandler(byte[] data);
        public event byteHandler recievingData;

        private Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        private string ip;
        private int port;

        private static Object Lock = new Object();
        private bool _connected = false;
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
                lock (Lock)
                {
                    _connected = value;
                }
            }
        }

        //log events
        public delegate void consoleLog(string message);
        public event consoleLog consoleLogged;

        #region Connect
        public void connect(string ip = "127.0.0.1", int port = 5000)
        {
            this.ip = ip;
            this.port = port;

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

            log($"Client set up to communicate with Server {ip} with Port {port}");
        }
        #endregion

        #region Recieve
        private void connectCallback(IAsyncResult ar)
        {
            try
            {
                Socket tempS = ar.AsyncState as Socket;
                tempS.EndConnect(ar);

                log("Connected.");
                connected = true;

                packageState package = new packageState(tempS);

                tempS.BeginReceive(package.sizeBuffer, 0, package.sizeBuffer.Length, SocketFlags.None, new AsyncCallback(recieveCallback), package);
            }
            catch
            {
                connected = false;
                log("Failed to connect to Server.");
            }
        }

        private void recieveCallback(IAsyncResult ar)
        {
            try
            {
                packageState package = ar.AsyncState as packageState;
                Socket clientS = package.socket;

                int bytesRead = clientS.EndReceive(ar);

                if (bytesRead > 0)
                {
                    int size = ATclient.BUFFER_SIZE;
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
                            byte[] temp = package.readBuffer.Clone() as byte[];

                            new Thread(() =>
                            {
                                recievingData?.Invoke(temp);
                            }).Start();

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
                        int readsize = (ATclient.BUFFER_SIZE > size) ? size : ATclient.BUFFER_SIZE;
                        package.socket.BeginReceive(package.readBuffer, package.readOffset, readsize, SocketFlags.None, new AsyncCallback(recieveCallback), package);
                    }
                }
                else
                {
                    log("Error recieving Message! ReadBytes < 0.");
                }
            }
            catch
            {
                log("Error recieving Message!");
            }
        }
        #endregion

        #region Send
        public void sendData(byte[] data)
        {
            if (connected)
            {
                try
                {
                    clientSocket.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(sendCallback), clientSocket);
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
            }
        }
        #endregion

        #region Handle Data
        public Dictionary<string, byteHandler> handleFunctions = new Dictionary<string, byteHandler>();

        private void handleData(byte[] data)
        {
            dataPackage pack = new dataPackage();
            pack.write(data);
            string enumString = pack.readString();
            pack.Dispose();

            byteHandler function;

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

    internal class packageState : IDisposable
    {
        public Socket socket = null;
        public byte[] sizeBuffer = new byte[ATclient.PACKAGE_LENGTH_SIZE];
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
