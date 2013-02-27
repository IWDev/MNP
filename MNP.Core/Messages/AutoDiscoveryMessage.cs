
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
            this.MessageType = BroadcastMessageType.None;
        }

        public AutoDiscoveryMessage(BroadcastMessageType msgType, EndPoint ip)
        {
            this.MessageType = msgType;
            this.IP = (IPEndPoint)ip;
        }

    }
}
