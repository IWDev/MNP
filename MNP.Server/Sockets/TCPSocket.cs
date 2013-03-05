using System.Linq;
using MNP.Core;
using MNP.Server.Providers;
using System;
using System.Collections.Generic;
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
        private readonly Socket _socket;
        private readonly ManagedStack<byte[]> _buffers = new ManagedStack<byte[]>();
        private readonly ManagedStack<SocketAsyncEventArgs> _saeas = new ManagedStack<SocketAsyncEventArgs>();
        private Int32 _connectedClients;
        private readonly List<SocketAsyncEventArgs> _connectedSockets = new List<SocketAsyncEventArgs>();
        private readonly object _locker = new Object();
        private bool _stopProcessingPackets;
        private readonly Dictionary<IPAddress, SocketAsyncEventArgs> _ipAddresses = new Dictionary<IPAddress, SocketAsyncEventArgs>();
        #endregion

        #region "Public Properties"
        public Int32 ConnectedClients { get { return _connectedClients; } }
        public Int32 Port { get; set; }
        private Int32 MessagePrefixLength { get; set; }
        public IPAddress BindingAddress { get; set; }
        private ILogProvider LogProvider { get; set; }
        public bool AllowAddressReuse { get; set; }
        public bool UseNaglesAlgorithm { get; set; }
        #endregion

        #region "Constructors"
        public TCPSocket(ILogProvider logger)
        {
            // check the interfaces
            LogProvider = logger ?? new DefaultLogProvider(LogLevel.Verbose);
            // use a default message prefix
            if (MessagePrefixLength <= 0)
            {
                MessagePrefixLength = 4;
            }

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, AllowAddressReuse ? 1 : 0);
            _socket.NoDelay = !UseNaglesAlgorithm;

            for (Int32 i = 0; i < 400; i++)
            {
                _buffers.Insert(new byte[512]);
                _saeas.Insert(new SocketAsyncEventArgs());
            }
        }
        #endregion

        #region "Event Handlers"
        private void SocketAccept_Completed(object sender, SocketAsyncEventArgs e)
        {
            LogProvider.Log("event entry", "TCPSocket.SocketAccept_Completed", LogLevel.Verbose);
            ClientAccepted(e);
        }

        private void SocketData_Completed(object sender, SocketAsyncEventArgs e)
        {
            LogProvider.Log("event entry", "TCPSocket.SocketData_Completed", LogLevel.Verbose);
            DataReceived(e);
        }
        #endregion

        #region "Internal handler methods"
        private void CloseSocket(Socket e)
        {
            LogProvider.Log("disconnecting clients", "TCPSocket.CloseSocket", LogLevel.Verbose);
            DisconnectClients();

            // do a shutdown before you close the socket
            try
            {
                LogProvider.Log("checking for existing connection on socket", "TCPSocket.CloseSocket", LogLevel.Verbose);
                if (e.Connected)
                {
                    LogProvider.Log("shutting down socket", "TCPSocket.CloseSocket", LogLevel.Verbose);
                    e.Shutdown(SocketShutdown.Both);
                }
            }
            // throws if socket was already closed
            catch (Exception ex)
            {
                LogProvider.Log("Error closing socket - " + ex.Message, "TCPSocket.CloseSocket", LogLevel.Minimal);
            }

            // Close the socket, which calls Dispose internally
            LogProvider.Log("closing socket, which calls dispose internally", "TCPSocket.CloseSocket", LogLevel.Verbose);
            e.Close();
        }

        private void BeginAccept(SocketAsyncEventArgs args)
        {
            LogProvider.Log("checking arguments", "TCPSocket.BeginAccept", LogLevel.Verbose);
            if (args == null)
            {
                args = _saeas.TakeNext();
                args.UserToken = new UserDataToken(args.RemoteEndPoint);
                args.Completed += SocketAccept_Completed;

                _connectedSockets.Add(args);
            }

            try
            {
                // Accept a connection and see if the event completed synchronously or not
                LogProvider.Log("attempting to accept a client asynchronously", "TCPSocket.BeginAccept", LogLevel.Verbose);
                if (!_socket.AcceptAsync(args))
                {
                    // We need to process the accept manually
                    LogProvider.Log("but completed synchronously", "TCPSocket.BeginAccept", LogLevel.Verbose);
                    ClientAccepted(args);
                }
            }
            catch
            {
                // we should only jump here when we disconnect all the clients.
                LogProvider.Log("exception thrown", "TCPSocket.BeginAccept", LogLevel.Verbose);
            }
        }

        private void ClientAccepted(SocketAsyncEventArgs e)
        {
            LogProvider.Log("client has been accepted, checking for socket error", "TCPSocket.ClientAccepted", LogLevel.Verbose);
            if (e.SocketError != SocketError.Success)
            {
                // Potential bad socket
                LogProvider.Log("potential bad socket", "TCPSocket.ClientAccepted", LogLevel.Verbose);                

                BeginAccept(e);

                return;
            }

            // make sure that the token has some data in it
            UserDataToken token = (UserDataToken)e.UserToken;
            if (token == null || token.Endpoint == null)
            {
                token = new UserDataToken(Extensions.Coalesce<Socket>(e.AcceptSocket, e.ConnectSocket, _socket).RemoteEndPoint);
            }
            LogProvider.Log("token parsing complete", "TCPSocket.ClientAccepted", LogLevel.Verbose);                

            // Increment the amount of connected clients
            lock (_locker)
            {
                Interlocked.Increment(ref _connectedClients);
                LogProvider.Log("Connected Clients = " + _connectedClients, "TCPSocket.ClientAccepted", LogLevel.Minimal);
            }
            LogProvider.Log("beginning new accept operation", "TCPSocket.ClientAccepted", LogLevel.Verbose);                

            // Now we have the connection, we can start a new accept operation
            BeginAccept(null);

            // re-assign the completed event handler for the SocketAsyncEventArgs
            e.UserToken = token;
            LogProvider.Log("token re-assigned", "TCPSocket.ClientAccepted", LogLevel.Verbose);                

            // Add the client to know clients
            if (!_ipAddresses.ContainsKey(token.Endpoint.Address))
            {
                LogProvider.Log("ip address added to known addresses", "TCPSocket.ClientAccepted", LogLevel.Verbose);
                _ipAddresses.Add(token.Endpoint.Address, e);
            }

            LogProvider.Log("starting the receive operation", "TCPSocket.ClientAccepted", LogLevel.Verbose);
            // Start receiving the data
            BeginReceive(e);
        }

        private void BeginReceive(SocketAsyncEventArgs e)
        {
            // see if we can prevent an operation exception
            LogProvider.Log("starting receive", "TCPSocket.BeginReceive", LogLevel.Verbose);
            if (((UserDataToken)e.UserToken).PendingReceiveOperation)
            {
                return;
            }

            byte[] buffer;

            // pattern to avoid duplicating events
            e.Completed -= SocketAccept_Completed;
            e.Completed -= SocketData_Completed;
            e.Completed += SocketData_Completed;
            LogProvider.Log("event handlers re-assigned", "TCPSocket.BeginReceive", LogLevel.Verbose);

            // set the buffer before we start
            if (e.Buffer != null)
            {
                buffer = e.Buffer;
                Array.Clear(buffer, 0, buffer.Length);
            }
            else
            {
                buffer = _buffers.TakeNext();
            }
            LogProvider.Log("buffer cleared/created", "TCPSocket.BeginReceive", LogLevel.Verbose);

            // reset the buffer
            e.SetBuffer(buffer, 0, buffer.Length);

            ((UserDataToken)e.UserToken).PendingReceiveOperation = true;
            LogProvider.Log("pending operation set", "TCPSocket.BeginReceive", LogLevel.Verbose);

            // Accept some data and see if the event completed synchronously or not
            LogProvider.Log("attempting to receive data asynchronously", "TCPSocket.BeginReceive", LogLevel.Verbose);
            if (!Extensions.Coalesce<Socket>(e.AcceptSocket, e.ConnectSocket, _socket).ReceiveAsync(e))
            {
                LogProvider.Log("but completed synchronously", "TCPSocket.BeginReceive", LogLevel.Verbose);
                // We need to process the data manually
                DataReceived(e);
            }
        }

        private void DataReceived(SocketAsyncEventArgs e)
        {
            LogProvider.Log("start", "TCPSocket.DataReceived", LogLevel.Verbose);
            if (e.SocketError != SocketError.Success || (e.BytesTransferred == 0 && e.LastOperation != SocketAsyncOperation.Accept))
            {
                // Close the connection, e.BytesTransferred == 0 indicates that the socket is being closed by the client, usually.
                _ipAddresses.Remove(((IPEndPoint)e.ConnectSocket.RemoteEndPoint).Address);
                Stop();
                return;
            }

            UserDataToken userToken = (UserDataToken)e.UserToken;
            userToken.PendingReceiveOperation = false;
            LogProvider.Log("token parsed", "TCPSocket.DataReceived", LogLevel.Verbose);

            if (userToken == null || _stopProcessingPackets)
            {
                // The client is in the process of being disconnected
                return;
            }
            LogProvider.Log("checking for first packet", "TCPSocket.DataReceived", LogLevel.Verbose);

            bool isFirstMessage = (userToken.ProcessedBytes == 0);
            Int32 bytesToProcess = e.BytesTransferred;
            Int32 bytesToProcessOffset = 0;

            // handle the prefix if neccessary
            if (isFirstMessage)
            {
                LogProvider.Log("this is the first packet", "TCPSocket.DataReceived", LogLevel.Verbose);
                // Store the message prefix as raw bytes
                byte[] arrPrefix = new byte[MessagePrefixLength];
                Buffer.BlockCopy(e.Buffer, 0, arrPrefix, 0, MessagePrefixLength);

                // Set the message length
                userToken.DataLength = BitConverter.ToInt32(arrPrefix, 0);

                LogProvider.Log("expected data length is " + userToken.DataLength, "TCPSocket.DataReceived", LogLevel.Verbose);

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
            LogProvider.Log("data copied to token buffer", "TCPSocket.DataReceived", LogLevel.Verbose);

            // Increment the ProcessedBytes so we can see whether or not we have the full message
            userToken.ProcessedBytes += bytesToProcess;

            if (userToken.ProcessedBytes == userToken.DataLength)
            {
                LogProvider.Log("all data received", "TCPSocket.DataReceived", LogLevel.Verbose);
                // Thread safe way of triggering the event
                var evnt = OnDataReceived;

                if (evnt != null)
                {
                    LogProvider.Log("event fired", "TCPSocket.DataReceived", LogLevel.Verbose);
                    evnt(e, new SocketDataEventArgs(userToken.DataBuffer, PacketType.TCP, userToken.Endpoint.Address));
                }

                // reset the token for the next message
                userToken.Reset();
                LogProvider.Log("token reset", "TCPSocket.DataReceived", LogLevel.Verbose);
            }

            // Start a new receive operation
            LogProvider.Log("starting another receive operation", "TCPSocket.DataReceived", LogLevel.Verbose);
            BeginReceive(e);
        }

        private void DisconnectClient(SocketAsyncEventArgs e)
        {
            LogProvider.Log("disconnecting client", "TCPSocket.DisconnectClient", LogLevel.Verbose);

            if (e == null)
            {
                return;
            }
            try
            {
                _socket.DisconnectAsync(e);
                // Clear the buffer so that we can re-use the resource
                if (e.Buffer != null)
                {
                    _buffers.Insert(e.Buffer);
                }
                _saeas.Insert(e);
            }
            catch
            {
                _saeas.Insert(e);
                if (e.Buffer != null)
                {
                    _buffers.Insert(e.Buffer);
                }
            }
            // Decrement the amount of connected clients
            lock (_locker)
            {
                Interlocked.Decrement(ref _connectedClients);
            }
        }

        private void DisconnectClients()
        {
            List<SocketAsyncEventArgs> copiedList = _connectedSockets;
            foreach (var e in copiedList)
            {
                DisconnectClient(e);
            }
            copiedList.Clear();
            _connectedSockets.Clear();
        }
        #endregion

        #region "ISocket implicit implementation"
        public void Start()
        {
            LogProvider.Log("Socket starting", "TCPSocket.Start", LogLevel.Verbose);
            _socket.Bind(new IPEndPoint(BindingAddress, Port));
            _stopProcessingPackets = false;
            _socket.Listen(100);
            BeginAccept(null);
        }

        public void Stop()
        {
            LogProvider.Log("Socket stopping", "TCPSocket.Stop", LogLevel.Verbose);

            _stopProcessingPackets = true;
            CloseSocket(_socket); // calls DisconnectClients internally
        }

        public bool SendTo(string address, byte[] data)
        {
            LogProvider.Log("Start", "TCPSocket.SendTo", LogLevel.Verbose);
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
            LogProvider.Log("arguments parsed", "TCPSocket.SendTo", LogLevel.Verbose);

            // check the ip address and send (TCP so we know the addresses)
            if (_ipAddresses.ContainsKey(ip))
            {
                LogProvider.Log("valid ip found", "TCPSocket.SendTo", LogLevel.Verbose);
                // send the socket args
                SocketAsyncEventArgs args = _ipAddresses[ip];
                // unhook the event args from the complete handler if it exists
                // http://stackoverflow.com/questions/1129517/c-sharp-how-to-find-if-an-event-is-hooked-up
                // "removing a handler that's not there is legal, and does nothing"
                args.Completed -= OnConnectToCompleted;

                LogProvider.Log("sending data synchronously", "TCPSocket.SendTo", LogLevel.Verbose);
                // send the data synchronously
                Extensions.Coalesce<Socket>(args.AcceptSocket, args.ConnectSocket, _socket).Send(data);

                // start the recieve operation
                LogProvider.Log("starting receieve operation", "TCPSocket.SendTo", LogLevel.Verbose);
                BeginReceive(args);

                return true;
            }
            LogProvider.Log("ip address not known", "TCPSocket.SendTo", LogLevel.Verbose);
            // this is not a known address
            return false;
        }

        public void ConnectTo(IPEndPoint ipe)
        {
            LogProvider.Log("start", "TCPSocket.ConnectTo", LogLevel.Verbose);
            _socket.Bind(new IPEndPoint(BindingAddress, Port));

            if (ipe == null)
            {
                throw new ArgumentException("The IPEndPoint cannot be null", "ipe");
            }

            SocketAsyncEventArgs e = _saeas.TakeNext();
            e.RemoteEndPoint = ipe;
            e.UserToken = new UserDataToken(ipe);
            e.Completed += OnConnectToCompleted;

            LogProvider.Log("attempting to connect asynchronously", "TCPSocket.ConnectTo", LogLevel.Verbose);
            if (!_socket.ConnectAsync(e))
            {
                LogProvider.Log("but completed synchronously", "TCPSocket.ConnectTo", LogLevel.Verbose);
                ConnectToCompleted(e);
            }
        }

        private void OnConnectToCompleted(object sender, SocketAsyncEventArgs e)
        {
            LogProvider.Log("connection completed asynchronously", "TCPSocket.OnConnectToCompleted", LogLevel.Verbose);

            ConnectToCompleted(e);
        }

        private void ConnectToCompleted(SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                return;
            }
            LogProvider.Log("start", "TCPSocket.ConnectToCompleted", LogLevel.Verbose);


            LogProvider.Log("checking remote endpoint", "TCPSocket.ConnectToCompleted", LogLevel.Verbose);
            // Add the client to know clients
            if (e.RemoteEndPoint != null)
            {
                LogProvider.Log("valid", "TCPSocket.ConnectToCompleted", LogLevel.Verbose);
                if (!_ipAddresses.ContainsKey(((IPEndPoint)e.RemoteEndPoint).Address))
                {
                    LogProvider.Log("added ip to collection", "TCPSocket.ConnectToCompleted", LogLevel.Verbose);
                    _ipAddresses.Add(((IPEndPoint)e.RemoteEndPoint).Address, e);
                }
            }

            var ev = OnClientConnectCompleted;

            if (ev != null)
            {
                LogProvider.Log("event fired", "TCPSocket.ConnectToCompleted", LogLevel.Verbose);
                ev(null, (IPEndPoint)e.RemoteEndPoint);
            }

        }

        public List<IPAddress> GetKnownNodes()
        {
            return _ipAddresses.Keys.ToList();
        }

        public event EventHandler<SocketDataEventArgs> OnDataReceived;

        public event EventHandler<IPEndPoint> OnClientConnectCompleted;
        #endregion
    }
}
