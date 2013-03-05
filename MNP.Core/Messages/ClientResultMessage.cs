
using System;
using System.Net;
namespace MNP.Core.Messages
{
    [Serializable]
    public class ClientResultMessage
    {
        public byte[] Data { get; set; }
        public IPAddress Source { get; set; }
    }
}
