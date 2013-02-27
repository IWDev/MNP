using System.Net;

namespace MNP.Server
{
    /// <summary>
    /// Internal Class used to maintain the TCP State
    /// </summary>
    internal sealed class UserDataToken
    {

        internal UserDataToken(EndPoint endpoint)
        {
            this.Endpoint = (IPEndPoint)endpoint;
        }

        internal IPEndPoint Endpoint { get; private set; }

        internal int ProcessedBytes { get; set; }

        internal int DataLength { get; set; }

        internal byte[] DataBuffer { get; set; }

        internal void Reset()
        {
            DataBuffer = null;
            DataLength = 0;
            ProcessedBytes = 0;
        }
    }
}
