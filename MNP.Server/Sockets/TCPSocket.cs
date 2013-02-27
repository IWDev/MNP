using MNP.Core;
using MNP.Server.Providers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MNP.Server
{
    /// <summary>
    /// Provides a basic implementation of a TCPSocket based on ISocket
    /// </summary>
    public class TCPSocket : ISocket
    {
        #region "Private Variables"
        private Socket _socket;
        private IBufferManagerProvider _bufferManager;
        private Int32 _connectedClients = 0;
        private List<SocketAsyncEventArgs> _connectedSockets = new List<SocketAsyncEventArgs>();
        private object _locker = new Object();
        private bool _stopProcessingPackets = false;
        private Dictionary<IPAddress, SocketAsyncEventArgs> _IpAddresses = new Dictionary<IPAddress, SocketAsyncEventArgs>();
        #endregion

        #region "Public Properties"
        public Int32 ConnectedClients { get { return _connectedClients; } }
        public Int32 Port { get; private set; }
        public Int32 MessagePrefixLength { get; set; }
        public Int32 BufferPreAllocationAmount { get; private set; }
        public IPAddress ListeningAddress { get; private set; }
        public ILogProvider LogProvider { get; private set; }
        public bool AllowAddressReuse { get; set; }
        public bool UseNaglesAlgorithm { get; private set; }
        #endregion

        #region "Constructors"
        private TCPSocket() { }
        public TCPSocket(String listeningAddress) : this(listeningAddress, 4444, null, null, true) { }
        public TCPSocket(String listeningAddress, Int32 port) : this(listeningAddress, port, null, null, true) { }
        public TCPSocket(String listeningAddress, Int32 port, Int32 bufferPreAllocationAmount) : this(listeningAddress, port, null, null, true) { }
        public TCPSocket(Int32 port) : this("0.0.0.0", port, null, null, true) { }

        public TCPSocket(String listeningAddress, Int32 port, IBufferManagerProvider manager, ILogProvider logger, bool useNagles)
        {
            // Setup the port
            if (port <= 0)
            {
                throw new ArgumentException("Port number cannot be less than 0.");
            }
            else
            {
                this.Port = port;
            }

            // check the ip address
            if (String.IsNullOrEmpty(listeningAddress))
            {
                throw new Exception("The listening address supplied is not valid.");
            }
            this.ListeningAddress = IPAddress.Parse(listeningAddress);

            // check the interfaces
            _bufferManager = (manager == null) ? new DefaultBufferManager(100, 2048, null, null) : manager;
            this.LogProvider = (logger == null) ? new DefaultLogProvider(LogLevel.None) : logger;

            // use a default message prefix
            this.MessagePrefixLength = 4;

            this.UseNaglesAlgorithm = false;// useNagles;

            this.LogProvider.Log("Starting. Creating socket", "TCPSocket.Constructor", LogLevel.Verbose);
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, this.AllowAddressReuse ? 1 : 0);
            _socket.NoDelay = !this.UseNaglesAlgorithm;
            _socket.Bind(new IPEndPoint(this.ListeningAddress, this.Port));
            this.LogProvider.Log("Socket created and bound", "TCPSocket.Constructor", LogLevel.Minimal);
        }
        #endregion

        #region "Event Handlers"
        private void SocketAccept_Completed(object sender, SocketAsyncEventArgs e)
        {
            this.LogProvider.Log("Event entry", "TCPSocket.SocketAccept_Completed", LogLevel.Verbose);
            ClientAccepted(e);
            this.LogProvider.Log("Event exit", "TCPSocket.SocketAccept_Completed", LogLevel.Verbose);
        }

        private void SocketData_Completed(object sender, SocketAsyncEventArgs e)
        {
            this.LogProvider.Log("Event entry", "TCPSocket.SocketData_Completed", LogLevel.Verbose);
            DataReceived(e);
            this.LogProvider.Log("Event exit", "TCPSocket.SocketData_Completed", LogLevel.Verbose);
        }
        #endregion

        #region "Internal handler methods"
        private void CloseSocket(Socket e)
        {
            DisconnectClients();

            // do a shutdown before you close the socket
            try
            {
                if (e.Connected)
                {
                    e.Shutdown(SocketShutdown.Both);
                }
                this.LogProvider.Log("Clean socket shutdown", "TCPSocket.CloseSocket", LogLevel.Verbose);
            }
            // throws if socket was already closed
            catch (Exception ex)
            {
                this.LogProvider.Log("Error closing socket - " + ex.Message, "TCPSocket.CloseSocket", LogLevel.Minimal);
            }

            // Close the socket, which calls Dispose internally
            e.Close();
            this.LogProvider.Log("Socket closed", "TCPSocket.CloseSocket", LogLevel.Verbose);
        }

        private void BeginAccept(SocketAsyncEventArgs args)
        {
            if (args == null)
            {
                args = _bufferManager.TakeNextSocketAsyncEventArgs();
                args.UserToken = new UserDataToken(args.RemoteEndPoint);
                args.Completed += SocketAccept_Completed;

                _connectedSockets.Add(args);
            }

            try
            {
                // Accept a connection and see if the event completed synchronously or not
                if (!_socket.AcceptAsync(args))
                {
                    // We need to process the accept manually
                    ClientAccepted(args);
                }
            }
            catch
            {
                // we should only jump here when we disconnect all the clients.
            }
        }

        private void ClientAccepted(SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                // Potential bad socket
                Debug.WriteLine("Socket error: " + e.SocketError.ToString());

                BeginAccept(e);

                return;
            }

            // make sure that the token has some data in it
            UserDataToken token = (UserDataToken)e.UserToken;
            if (token == null || token.Endpoint == null)
            {
                token = new UserDataToken(Extensions.Coalesce<Socket>(e.AcceptSocket, e.ConnectSocket, _socket).RemoteEndPoint);
            }

            // Increment the amount of connected clients
            lock (_locker)
            {
                Interlocked.Increment(ref _connectedClients);
                this.LogProvider.Log("Connected Clients = " + _connectedClients.ToString(), "TCPSocket.ClientAccepted", LogLevel.Minimal);
            }

            // Now we have the connection, we can start a new accept operation
            BeginAccept(null);

            // re-assign the completed event handler for the SocketAsyncEventArgs
            e.Completed -= SocketAccept_Completed;
            e.Completed += SocketData_Completed;
            e.UserToken = token;

            // Add the client to know clients
            if (!_IpAddresses.ContainsKey(token.Endpoint.Address))
            {
                _IpAddresses.Add(token.Endpoint.Address, e);
            }

            // Start receiving the data
            BeginReceive(e);
        }

        private void BeginReceive(SocketAsyncEventArgs e)
        {
            byte[] buffer;

            // set the buffer before we start
            if (e.Buffer != null)
            {
                buffer = e.Buffer;
                Array.Clear(buffer, 0, buffer.Length);
            }
            else
            {
                buffer = _bufferManager.TakeNextBuffer();
            }

            // reset the buffer
            e.SetBuffer(buffer, 0, buffer.Length);

            // Accept some data and see if the event completed synchronously or not
            if (!Extensions.Coalesce<Socket>(e.AcceptSocket, e.ConnectSocket, _socket).ReceiveAsync(e))
            {
                // We need to process the data manually
                DataReceived(e);
            }
        }

        private void DataReceived(SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success || (e.BytesTransferred == 0 && e.LastOperation != SocketAsyncOperation.Accept))
            {
                // Close the connection, e.BytesTransferred == 0 indicates that the socket is being closed by the client, usually.
                _IpAddresses.Remove(((IPEndPoint)e.ConnectSocket.RemoteEndPoint).Address);
                Stop();
                return;
            }

            UserDataToken userToken = (UserDataToken)e.UserToken;

            if (userToken == null || _stopProcessingPackets)
            {
                // The client is in the process of being disconnected
                return;
            }

            bool isFirstMessage = (userToken.ProcessedBytes == 0);
            Int32 bytesToProcess = e.BytesTransferred;
            Int32 bytesToProcessOffset = 0;

            // handle the prefix if neccessary
            if (isFirstMessage)
            {
                // Store the message prefix as raw bytes
                byte[] arrPrefix = new byte[MessagePrefixLength];
                Buffer.BlockCopy(e.Buffer, 0, arrPrefix, 0, MessagePrefixLength);

                // Set the message length
                userToken.DataLength = BitConverter.ToInt32(arrPrefix, 0);

                // Remove the amount of bytes to process
                bytesToProcess -= MessagePrefixLength;
                bytesToProcessOffset = MessagePrefixLength;

                // Setup the data buffer to store the whole data
                userToken.DataBuffer = new byte[userToken.DataLength];

                // If we just have the header of the message then start another receive operation
                if (bytesToProcess == 0)
                {
                    BeginReceive(e);
                    return;
                }
            }

            // Copy the remaining data to the data buffer on the user token
            Buffer.BlockCopy(e.Buffer, bytesToProcessOffset, userToken.DataBuffer, userToken.ProcessedBytes, bytesToProcess);

            // Increment the ProcessedBytes so we can see whether or not we have the full message
            userToken.ProcessedBytes += bytesToProcess;

            if (userToken.ProcessedBytes == userToken.DataLength)
            {
                // Thread safe way of triggering the event
                var evnt = OnDataReceived;

                if (evnt != null)
                {
                    evnt(e, new SocketDataEventArgs(userToken.DataBuffer, PacketType.TCP, userToken.Endpoint.Address));
                }

                // reset the token for the next message
                userToken.Reset();
            }

            // Start a new receive operation
            BeginReceive(e);
        }

        private void DisconnectClient(SocketAsyncEventArgs e)
        {
            if (e == null)
            {
                return;
            }
            try
            {
                _socket.DisconnectAsync(e);
                this.LogProvider.Log("Server forced client disconnect", "TCPSocket.DisconnectClient", LogLevel.Verbose);
                // Clear the buffer so that we can re-use the resource
                if (e.Buffer != null)
                {
                    _bufferManager.InsertBuffer(e.Buffer);
                    this.LogProvider.Log("Buffer re-pushed", "TCPSocket.DisconnectClient", LogLevel.Verbose);
                }
                // The buffer manager will reset the event args for us...
                _bufferManager.InsertSocketAsyncEventArgs(e);
            }
            catch
            {
                this.LogProvider.Log("Server forced client disconnect dirtly", "TCPSocket.DisconnectClient", LogLevel.Verbose);
                _bufferManager.InsertSocketAsyncEventArgs(e);
                _bufferManager.InsertBuffer(e.Buffer);
            }
            // Decrement the amount of connected clients
            lock (_locker)
            {
                Interlocked.Decrement(ref _connectedClients);
                this.LogProvider.Log("Connected Clients = " + _connectedClients.ToString(), "TCPSocket.CloseSocket", LogLevel.Minimal);
            }
        }

        public void DisconnectClients()
        {
            List<SocketAsyncEventArgs> copiedList = _connectedSockets;
            foreach (var e in copiedList)
            {
                DisconnectClient(e);
            }
            copiedList.Clear();
            _connectedSockets.Clear();
            this.LogProvider.Log("All clients disconnected and lists cleared", "TCPSocket.DisconnectClients", LogLevel.Verbose);
        }
        #endregion

        #region "ISocket implicit implementation"
        public void Start()
        {
            _stopProcessingPackets = false;
            _socket.Listen(100);
            BeginAccept(null);
            this.LogProvider.Log("Accepting connections", "TCPSocket.Start", LogLevel.Verbose);
        }

        public void Stop()
        {
            _stopProcessingPackets = true;
            this.LogProvider.Log("Stopping. Closing connections", "TCPSocket.Stop", LogLevel.Minimal);
            CloseSocket(_socket); // calls DisconnectClients internally
            this.LogProvider.Log("Accepting connections", "TCPSocket.Stop", LogLevel.Verbose);
        }

        public bool SendTo(string address, byte[] data)
        {
            // check the arguments
            if (String.IsNullOrEmpty(address))
            {
                throw new ArgumentException("Invalid address", "address");
            }

            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Invalid data message", "data");
            }

            // parse the ip address
            IPAddress ip = IPAddress.Parse(address);

            // check the ip address and send (TCP so we know the addresses)
            if (_IpAddresses.ContainsKey(ip))
            {
                // take and prepare a new socket async event args
                SocketAsyncEventArgs e = _bufferManager.TakeNextSocketAsyncEventArgs();
                e.SetBuffer(data, 0, data.Length);

                // send the socket args

                SocketAsyncEventArgs args = _IpAddresses[ip]; 
                Extensions.Coalesce<Socket>(args.AcceptSocket, args.ConnectSocket, _socket).SendAsync(e);

                // return the args back to the pool
                _bufferManager.InsertSocketAsyncEventArgs(e);

                return true;
            }
            else
            {
                // this is not a known address
                return false;
            }
        }

        public void ConnectTo(IPEndPoint ipe)
        {
            if (ipe == null)
            {
                throw new ArgumentException("The IPEndPoint cannot be null", "ipe");
            }

            SocketAsyncEventArgs e = _bufferManager.TakeNextSocketAsyncEventArgs();
            e.RemoteEndPoint = ipe;
            e.UserToken = new UserDataToken(ipe);
            e.Completed += OnConnectToCompleted;

            if (!_socket.ConnectAsync(e))
            {
                ConnectToCompleted(e);
            }
        }

        private void OnConnectToCompleted(object sender, SocketAsyncEventArgs e)
        {
            ConnectToCompleted(e);
        }

        private void ConnectToCompleted(SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                return;
            }

            // Add the client to know clients
            if (e.RemoteEndPoint != null)
            {
                if (!_IpAddresses.ContainsKey(((IPEndPoint)e.RemoteEndPoint).Address))
                {
                    _IpAddresses.Add(((IPEndPoint)e.RemoteEndPoint).Address, e);
                }
            }

            var ev = OnClientConnectCompleted;

            if (ev != null)
            {
                ev(null, (IPEndPoint)e.RemoteEndPoint);
            }

            ClientAccepted(e);
        }

        public List<IPAddress> GetKnownNodes()
        {
            List<IPAddress> res = new List<IPAddress>();
            foreach (IPAddress el in _IpAddresses.Keys)
            {
                res.Add(el);
            }
            return res;
        }

        public event EventHandler<SocketDataEventArgs> OnDataReceived;

        public event EventHandler<IPEndPoint> OnClientConnectCompleted;
        #endregion
    }
}
