using MNP.Core;

namespace MNP.Server.Providers
{
    public sealed class DefaultStorageProvider : IStorageProvider
    {
        // TODO :: implement interface 

        public void Write(string key, byte[] data)
        {
            throw new System.NotImplementedException();
        }

        public void Write(string key, string str)
        {
            throw new System.NotImplementedException();
        }

        public System.IO.Stream Read(string key)
        {
            throw new System.NotImplementedException();
        }
    }
}
