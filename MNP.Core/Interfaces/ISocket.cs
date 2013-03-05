using System;
using System.Collections.Generic;
using System.Net;

namespace MNP.Core
{
    /// <summary>
    /// Provides the methods required to maintain a socket
    /// </summary>
    public interface ISocket
    {
        /// <summary>
        /// Starts the socket
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the socket
        /// </summary>
        void Stop();

        /// <summary>
        /// Whether or not to set the ReuseAddress socket option
        /// </summary>
        bool AllowAddressReuse { get; set; }

        Int32 Port { get; set; }

        IPAddress BindingAddress { get; set; }

        /// <summary>
        /// Send data to a specific address
        /// </summary>
        /// <param name="address">The address to send to</param>
        /// <param name="data">The data to send</param>
        bool SendTo(String address, byte[] data);

        /// <summary>
        /// Connect to a specific end point
        /// </summary>
        /// <param name="ipe"></param>
        void ConnectTo(IPEndPoint ipe);

        /// <summary>
        /// Returns a list of known nodes
        /// </summary>
        /// <returns></returns>
        List<IPAddress> GetKnownNodes();

        /// <summary>
        /// Fired when a complete message is received by the socket
        /// </summary>
        event EventHandler<SocketDataEventArgs> OnDataReceived;

        event EventHandler<IPEndPoint> OnClientConnectCompleted;
    }
}
