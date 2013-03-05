using System;
using System.ComponentModel.Composition;
using MNP.Core;

namespace MNP.PluginSample
{
    [Export(typeof(ILogProvider))]
    public sealed class LogProviderSample : ILogProvider
    {
        public IStorageProvider StorageProvide
        {
            get { throw new NotImplementedException(); }
        }

        public void Log(string message, string source, LogLevel LoggingLevel)
        {
            throw new NotImplementedException();
        }
    }
}
