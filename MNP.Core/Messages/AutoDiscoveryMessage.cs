
using System;
using System.Net;
namespace MNP.Core.Messages
{
    [Serializable]
    public sealed class AutoDiscoveryMessage
    {
        public BroadcastMessageType MessageType { get; set; }
        public IPEndPoint IP { get; set; }

        public AutoDiscoveryMessage()
        {
            MessageType = BroadcastMessageType.None;
        }

        public AutoDiscoveryMessage(BroadcastMessageType msgType, EndPoint ip)
        {
            MessageType = msgType;
            IP = (IPEndPoint)ip;
        }

    }
}
