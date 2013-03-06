using System.ComponentModel.Composition;
using MNP.Core;

namespace MNP.PluginSample
{
    [Export(typeof(IStorageProvider))]
    public sealed class StorageProviderSample : IStorageProvider
    {
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
