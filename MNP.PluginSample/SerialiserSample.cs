using System;
using System.ComponentModel.Composition;
using MNP.Core;

namespace MNP.PluginSample
{
    [Export(typeof(ISerialiser<T,Y>))]
    public sealed class SerialiserSample<T,Y> : ISerialiser<T, Y>
    {
        public Y Serialise(T source)
        {
            throw new NotImplementedException();
        }

        public T Deserialise(Y source)
        {
            throw new NotImplementedException();
        }
    }
}
