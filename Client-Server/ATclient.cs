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

        coming soon
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
        public bool connected
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

                if(value == false){
                    disconnecting?.Invoke();
                }
            }
        }

        public delegate void standardHandler();
        public event standardHandler disconnecting;

        //log events
        public delegate void consoleLog(string message);
        public event consoleLog consoleLogging;

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
                            //clone array so the package can be disposed
                            byte[] temp = package.readBuffer.Clone() as byte[];

                            //invoke the event
                            recievingData?.Invoke(temp);

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
                        int readsize = (ATclient.BUFFER_SIZE > size) ? size : ATclient.BUFFER_SIZE;
                        package.socket.BeginReceive(package.readBuffer, package.readOffset, readsize, SocketFlags.None, new AsyncCallback(recieveCallback), package);
                    }
                }
                else
                {
                    connected = false;
                    log("Error recieving Message! ReadBytes < 0.");
                }
            }
            catch
            {
                connected = false;
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
                    //send sizeinfo
                    byte[] sizeInfo = BitConverter.GetBytes(data.Length);

                    sendState state = new sendState();
                    state.socket = clientSocket;
                    state.data = data;

                    //send sizeinfo
                    clientSocket.BeginSend(sizeInfo, 0, sizeInfo.Length, SocketFlags.None, new AsyncCallback(sendCallback), state);
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
                sendState state = ar.AsyncState as sendState;
                int sizeSend = state.socket.EndSend(ar);

                if (!state.sent)
                {
                    log("Sizeinfo sent.");
                    state.sent = true;
                    //sending actual data
                    state.socket.BeginSend(state.data, 0, state.data.Length, SocketFlags.None, new AsyncCallback(sendCallback), state);
                }
                else
                {
                    log($"Sent {sizeSend} Bytes to the Server.");
                    state.Dispose();
                }
            }
            catch
            {
                connected = false;
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
            consoleLogging?.Invoke(message);
        }
    }

    internal class sendState : IDisposable
    {
        public Socket socket = null;
        public byte[] data = null;
        public bool sent = false;

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    socket = null;
                    data = null;
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
