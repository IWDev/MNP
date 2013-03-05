using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using MNP.Core;

namespace MNP.PluginSample
{
    [Export(typeof(INodeTask))]
    public sealed class NodeTaskSample : INodeTask
    {
        public Task<byte[]> Execute(byte[] data)
        {
            throw new NotImplementedException();
        }
    }
}
