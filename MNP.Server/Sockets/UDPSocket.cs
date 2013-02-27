﻿using MNP.Core;
using MNP.Core.Messages;
using MNP.Server.Providers;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace MNP.Server
{
    /// <summary>
    /// Provides a basic implementation of a UDPSocket based on ISocket
    /// </summary>
    public class UDPSocket : IBroadcastSocket
    {
        #region "Private Variables"
        private Socket _socket;
        private IBufferManagerProvider _bufferManager;
        private ManagedStack<byte[]> _tempBuffers = new ManagedStack<byte[]>(400, false);
        private PacketType _type = PacketType.UDP;
        #endregion

        #region "Public Properties"
        public Int32 Port { get; private set; }
        public Int32 MessagePrefixLength { get; set; }
        public IPAddress ListeningAddress { get; private set; }
        public IPAddress BroadcastSourceAddress { get { return this.ListeningAddress; } }
        public ILogProvider LogProvider { get; private set; }
        public bool AllowAddressReuse { get; set; }
        public bool UseBroadcasting { get; set; }
        #endregion

        #region "Constructors"
        private UDPSocket() { }
        public UDPSocket(String listeningAddress) : this(listeningAddress, 4444, null, null) { }
        public UDPSocket(String listeningAddress, Int32 port) : this(listeningAddress, port, null, null) { }
        public UDPSocket(Int32 port) : this("0.0.0.0", port, null, null) { }

        public UDPSocket(String listeningAddress, Int32 port, IBufferManagerProvider manager, ILogProvider logger)
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
            this.ListeningAddress = (listeningAddress == "0.0.0.0") ? IPAddress.Any : IPAddress.Parse(listeningAddress);

            // check the interfaces
            this.LogProvider = (logger == null) ? new DefaultLogProvider(LogLevel.None) : logger;
            _bufferManager = (manager == null) ? new DefaultBufferManager(100, 2048, null, null) : manager;

            // use a default message prefix if not set
            if (this.MessagePrefixLength <= 0)
            {
                this.MessagePrefixLength = 4;
            }

            // fill the temporary buffers ready
            for (Int32 i = 0; i < 400; i++)
            {
                _tempBuffers.Insert(new byte[this.MessagePrefixLength]);
            }
        }
        #endregion

        #region "Event Handlers"
        #endregion

        #region "Internal handler methods"
        private void Receive()
        {
            SocketAsyncEventArgs args = _bufferManager.TakeNextSocketAsyncEventArgs();
            byte[] buff = _bufferManager.TakeNextBuffer();
            args.SetBuffer(buff, 0, buff.Length);
            args.Completed += PacketReceived;
            args.RemoteEndPoint = new IPEndPoint(this.ListeningAddress, this.Port);

            try
            {
                if (!_socket.ReceiveMessageFromAsync(args))
                {
                    OnPacketReceived(args);
                }
            }
            catch (Exception ex)
            {
                // we should only jump here when we disconnect all the clients.
                Console.WriteLine(ex.Message);
            }
        }

        private void PacketReceived(object sender, SocketAsyncEventArgs e)
        {
            OnPacketReceived(e);
        }

        private void OnPacketReceived(SocketAsyncEventArgs e)
        {
            // Start a new Receive operation straight away
            Receive();

            // Now process the packet that we have already
            if (e.BytesTransferred <= MessagePrefixLength)
            {
                // Error condition, empty packet
                this.LogProvider.Log(String.Format("Empty packet received from {0}. Discarding packet.", e.ReceiveMessageFromPacketInfo.Address.ToString()), "UDPSocket.OnPacketReceived", LogLevel.Minimal);
                ReleaseArgs(e);
                return;
            }

            // Make sure that we aren't the source of the message.
            if (e.ReceiveMessageFromPacketInfo.Address == ((IPEndPoint)_socket.LocalEndPoint).Address)
            {
                this.LogProvider.Log("Received packet from this node. Disregarding.", "UDPSocket.OnPacketReceived", LogLevel.Verbose);
                ReleaseArgs(e);
                return;
            }

            // Get the message length from the beginning of the packet.
            byte[] arrPrefix = _tempBuffers.TakeNext();
            Buffer.BlockCopy(e.Buffer, e.Offset, arrPrefix, 0, MessagePrefixLength);
            Int32 messageLength = BitConverter.ToInt32(arrPrefix, 0);

            // clear and return the buffer asap
            Array.Clear(arrPrefix, 0, this.MessagePrefixLength);
            _tempBuffers.Insert(arrPrefix);

            // the number of bytes remaining to store
            Int32 bytesToProcess = e.BytesTransferred - MessagePrefixLength;

            if (bytesToProcess < messageLength)
            {
                this.LogProvider.Log(String.Format("Missing data from {0}. Discarding packet.", e.ReceiveMessageFromPacketInfo.Address.ToString()), "UDPSocket.OnPacketReceived", LogLevel.Minimal);
                ReleaseArgs(e);
                return;
            }

            // Create a data buffer
            byte[] data = _bufferManager.TakeNextBuffer(); // new byte[messageLength];

            // Copy the remaining data to the data buffer on the user token
            Buffer.BlockCopy(e.Buffer, e.Offset + MessagePrefixLength, data, 0, messageLength);

            // Thread safe way of triggering the event
            var evnt = OnDataReceived;

            if (evnt != null)
            {
                evnt(e, new SocketDataEventArgs(data, _type, e.ReceiveMessageFromPacketInfo.Address));
                _bufferManager.InsertBuffer(data);
            }

            // Data is safely stored, so unhook the event and return the SocketAsyncEventArgs back to the pool
            ReleaseArgs(e);
        }

        private void ReleaseArgs(SocketAsyncEventArgs e)
        {
            e.Completed -= PacketReceived;
            _bufferManager.InsertSocketAsyncEventArgs(e);
            _bufferManager.InsertBuffer(e.Buffer);
        }
        #endregion

        #region "ISocket implicit implementation"
        public void Start()
        {
            if (this.UseBroadcasting && this.ListeningAddress == IPAddress.Any)
            {
                throw new NotSupportedException("Broadcasting can only be used on a specific interface. Please set the Server Binding Address properly.");
            }

            this.LogProvider.Log("Starting. Creating socket", "UDPSocket.Start", LogLevel.Verbose);
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.PacketInformation, true);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, this.AllowAddressReuse ? 1 : 0);
            _socket.Bind(new IPEndPoint(this.ListeningAddress, this.Port));
            //_socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, this.UseBroadcasting ? 1 : 0);
            
            if (this.UseBroadcasting)
            {
                _type = PacketType.BroadcastUDP;
            }

            // use a default message prefix
            this.MessagePrefixLength = 4;

            // begin receiving packets
            Receive();

            this.LogProvider.Log("Socket created. Listening for packets", "UDPSocket.Start", LogLevel.Verbose);
        }

        public void Stop()
        {
            // do a shutdown before you close the socket
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
                this.LogProvider.Log("Clean socket shutdown", "TCPSocket.CloseSocket", LogLevel.Verbose);
            }
            // throws if socket was already closed
            catch (Exception ex)
            {
                this.LogProvider.Log("Error closing socket - " + ex.Message, "TCPSocket.CloseSocket", LogLevel.Minimal);
            }

            // Close the socket, which calls Dispose internally
            _socket.Close();
            this.LogProvider.Log("Socket closed", "TCPSocket.CloseSocket", LogLevel.Verbose);
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

            // take and prepare the args
            SocketAsyncEventArgs e = _bufferManager.TakeNextSocketAsyncEventArgs();
            e.SetBuffer(data, 0, data.Length);

            // Set the end point
            e.RemoteEndPoint = new IPEndPoint(ip, this.Port);

            // send the data
            _socket.SendAsync(e);

            return true;
        }

        public void ConnectTo(IPEndPoint ipe)
        {
            throw new NotSupportedException();
        }

        public List<IPAddress> GetKnownNodes()
        {
            return null;
        }

        public event EventHandler<SocketDataEventArgs> OnDataReceived;
        #endregion

        #region "Broadcast implementation"

        public void SendBroadcastMessage(BroadcastMessageType msgType, ISerialiser<AutoDiscoveryMessage, byte[]> serialiser, Int32 multicastPort)
        {
            if (msgType == BroadcastMessageType.None)
            {
                throw new InvalidOperationException("The specified BroadcastMessageType is not valid");
            }

            if (serialiser == null)
            {
                throw new ArgumentException("The specified serialiser is not valid");
            }

            // Create and prepare the arguments
            byte[] temp = serialiser.Serialise(new AutoDiscoveryMessage(msgType, _socket.LocalEndPoint));
            byte[] data = BitConverter.GetBytes(temp.Length).Merge(temp);
            // send the data
            UdpClient cli = new UdpClient();
            cli.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, this.Port));
            
        }
        #endregion

        /// <summary>
        /// This event is not supported.
        /// </summary>
        public event EventHandler<IPEndPoint> OnClientConnectCompleted;
    }
}
