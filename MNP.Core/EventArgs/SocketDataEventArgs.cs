using System;
using System.Net;

namespace MNP.Core
{
    /// <summary>
    /// An inherited EventArgs that supplies the data received from an ISocket instance
    /// </summary>
    public class SocketDataEventArgs : EventArgs
    {
        /// <summary>
        /// The data that was returned from the ISocket instance
        /// </summary>
        public byte[] Data { get; private set; }
        /// <summary>
        /// Determines the type of packet received
        /// </summary>
        public PacketType Type { get; private set; }

        /// <summary>
        /// The source of the data packet
        /// </summary>
        public IPAddress Source { get; private set; }

        private SocketDataEventArgs()
        {}

        public SocketDataEventArgs(byte[] data, PacketType type, IPAddress source)
        {
            this.Data = data;
            this.Type = type;
            this.Source = source;
        }
    }
}
