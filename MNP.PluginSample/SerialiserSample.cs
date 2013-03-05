using System;
using System.ComponentModel.Composition;
using MNP.Core;

namespace MNP.PluginSample
{
#warning "Disabled the line below as cannot export generic interface through MEF"
    //[Export(typeof(ISerialiser<T, Y>))]
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
