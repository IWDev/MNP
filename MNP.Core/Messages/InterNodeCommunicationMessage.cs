
using MNP.Core.Enums;
using System;
namespace MNP.Core.Messages
{
    [Serializable]
    public class InterNodeCommunicationMessage
    {
        public InterNodeMessageType MessageType { get; set; }
        public byte[] Data { get; set; }
        public bool IsLocalOnly { get; set; }
        public string Tag { get; set; }
    }
}
