
using MNP.Core.Enums;
using System;
namespace MNP.Core.Messages
{
    [Serializable]
    public class ClientMessage
    {
        public ClientMessageType MessageType { get; set; }
        public byte[] Data { get; set; }
        public string Tag { get; set; }
    }
}
