
using System;
using System.Net;
namespace MNP.Core.Messages
{
    [Serializable]
    public sealed class AutoDiscoveryMessage
    {
        public BroadcastMessageType MessageType { get; set; }
        public IPEndPoint IP { get; set; }

        public AutoDiscoveryMessage(EndPoint ip, BroadcastMessageType msgType = BroadcastMessageType.None)
        {
            MessageType = msgType;
            IP = (IPEndPoint)ip;
        }

    }
}
