using System.ComponentModel.Composition;
using MNP.Core;

namespace MNP.PluginSample
{
    [Export(typeof(IStorageProvider))]
    public sealed class StorageProviderSample : IStorageProvider
    {
    }
}
