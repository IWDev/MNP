using MNP.Core;
using MNP.Core.Messages;
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
        private readonly ManagedStack<byte[]> _buffers = new ManagedStack<byte[]>();
        private readonly ManagedStack<SocketAsyncEventArgs> _saeas = new ManagedStack<SocketAsyncEventArgs>();
        private readonly ManagedStack<byte[]> _tempBuffers = new ManagedStack<byte[]>();
        private PacketType _type = PacketType.UDP;
        #endregion

        #region "Public Properties"
        public Int32 Port { get; set; }
        private Int32 MessagePrefixLength { get; set; }
        public IPAddress BindingAddress { get; set; }
        public IPAddress BroadcastSourceAddress { get { return BindingAddress; } }
        private ILogProvider LogProvider { get; set; }
        public bool AllowAddressReuse { get; set; }
        public bool UseBroadcasting { get; set; }
        #endregion

        #region "Constructors"
        public UDPSocket(ILogProvider logger)
        {
            // check the interfaces
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            LogProvider = logger;

            // use a default message prefix if not set
            if (MessagePrefixLength <= 0)
            {
                MessagePrefixLength = 4;
            }

            // fill the temporary buffers ready
            for (Int32 i = 0; i < 400; i++)
            {
                _tempBuffers.Insert(new byte[MessagePrefixLength]);
                _buffers.Insert(new byte[512]);
                _saeas.Insert(new SocketAsyncEventArgs());
            }
        }
        #endregion

        #region "Event Handlers"
        #endregion

        #region "Internal handler methods"
        private void Receive()
        {
            SocketAsyncEventArgs args = _saeas.TakeNext();
            byte[] buff = _buffers.TakeNext();
            args.SetBuffer(buff, 0, buff.Length);
            args.Completed += PacketReceived;
            args.RemoteEndPoint = new IPEndPoint(BindingAddress, Port);

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

            if (e.ReceiveMessageFromPacketInfo.Address == null)
            {
                // this needs a permenant fix
                Console.WriteLine("Null address, exiting...");
                return;
            }


            // Now process the packet that we have already
            if (e.BytesTransferred <= MessagePrefixLength)
            {
                // Error condition, empty packet
                LogProvider.Log(String.Format("Empty packet received from {0}. Discarding packet.", e.ReceiveMessageFromPacketInfo.Address), "UDPSocket.OnPacketReceived", LogLevel.Minimal);
                ReleaseArgs(e);
                return;
            }

            // Make sure that we aren't the source of the message.
            if (e.ReceiveMessageFromPacketInfo.Address.Equals(((IPEndPoint)_socket.LocalEndPoint).Address))
            {
                LogProvider.Log("Received packet from this node. Disregarding.", "UDPSocket.OnPacketReceived", LogLevel.Verbose);
                ReleaseArgs(e);
                return;
            }

            // Get the message length from the beginning of the packet.
            byte[] arrPrefix = _tempBuffers.TakeNext();
            Buffer.BlockCopy(e.Buffer, e.Offset, arrPrefix, 0, MessagePrefixLength);
            Int32 messageLength = BitConverter.ToInt32(arrPrefix, 0);

            // clear and return the buffer asap
            Array.Clear(arrPrefix, 0, MessagePrefixLength);
            _tempBuffers.Insert(arrPrefix);

            // the number of bytes remaining to store
            Int32 bytesToProcess = e.BytesTransferred - MessagePrefixLength;

            if (bytesToProcess < messageLength)
            {
                LogProvider.Log(String.Format("Missing data from {0}. Discarding packet.", e.ReceiveMessageFromPacketInfo.Address), "UDPSocket.OnPacketReceived", LogLevel.Minimal);
                ReleaseArgs(e);
                return;
            }

            // Create a data buffer
            byte[] data = _buffers.TakeNext(); // new byte[messageLength];

            // Copy the remaining data to the data buffer on the user token
            Buffer.BlockCopy(e.Buffer, e.Offset + MessagePrefixLength, data, 0, messageLength);

            // Thread safe way of triggering the event
            var evnt = OnDataReceived;

            if (evnt != null)
            {
                evnt(e, new SocketDataEventArgs(data, _type, e.ReceiveMessageFromPacketInfo.Address));
                _buffers.Insert(data);
            }

            // Data is safely stored, so unhook the event and return the SocketAsyncEventArgs back to the pool
            ReleaseArgs(e);
        }

        private void ReleaseArgs(SocketAsyncEventArgs e)
        {
            e.Completed -= PacketReceived;
            _saeas.Insert(e);
            _buffers.Insert(e.Buffer);
        }
        #endregion

        #region "ISocket implicit implementation"
        public void Start()
        {
            if (UseBroadcasting && BindingAddress.Equals(IPAddress.Any))
            {
                throw new NotSupportedException("Broadcasting can only be used on a specific interface. Please set the Server Binding Address properly.");
            }

            LogProvider.Log("Starting. Creating socket", "UDPSocket.Start", LogLevel.Verbose);
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, AllowAddressReuse ? 1 : 0);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, UseBroadcasting ? 1 : 0);
            _socket.Bind(new IPEndPoint(BindingAddress, Port));
            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.PacketInformation, true);

            if (UseBroadcasting)
            {
                _type = PacketType.BroadcastUDP;
            }

            // use a default message prefix
            MessagePrefixLength = 4;

            // begin receiving packets
            Receive();

            LogProvider.Log("Socket created. Listening for packets", "UDPSocket.Start", LogLevel.Verbose);
        }

        public void Stop()
        {
            // do a shutdown before you close the socket
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
                LogProvider.Log("Clean socket shutdown", "TCPSocket.CloseSocket", LogLevel.Verbose);
            }
            // throws if socket was already closed
            catch (Exception ex)
            {
                LogProvider.Log("Error closing socket - " + ex.Message, "TCPSocket.CloseSocket", LogLevel.Minimal);
            }

            // Close the socket, which calls Dispose internally
            _socket.Close();
            LogProvider.Log("Socket closed", "TCPSocket.CloseSocket", LogLevel.Verbose);
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
            SocketAsyncEventArgs e = _saeas.TakeNext();
            e.SetBuffer(data, 0, data.Length);

            // Set the end point
            e.RemoteEndPoint = new IPEndPoint(ip, Port);

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
            byte[] temp = serialiser.Serialise(new AutoDiscoveryMessage(_socket.LocalEndPoint, msgType));
            byte[] data = BitConverter.GetBytes(temp.Length).Merge(temp);
            // send the data
            UdpClient cli = new UdpClient();
            cli.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, Port));
            
        }
        #endregion

        /// <summary>
        /// This event is not supported.
        /// </summary>
        public event EventHandler<IPEndPoint> OnClientConnectCompleted;
    }
}
