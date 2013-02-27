using MNP.Core.Messages;
using System;
using System.Net;

namespace MNP.Core
{
    public interface IBroadcastSocket : ISocket
    {
        IPAddress BroadcastSourceAddress { get; }
        void SendBroadcastMessage(BroadcastMessageType msgType, ISerialiser<AutoDiscoveryMessage, byte[]> serialiser, Int32 multicastPort);
        bool UseBroadcasting { get; set; }
    }
}
