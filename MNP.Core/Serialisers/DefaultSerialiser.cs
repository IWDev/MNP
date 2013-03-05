using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace MNP.Core.Serialisers
{
    public sealed class DefaultSerialiser<T> : ISerialiser<T, byte[]> where T : class
    {
        private readonly BinaryFormatter _formatter = new BinaryFormatter();
        
        public byte[] Serialise(T source)
        {
            if (source == null)
            {
                throw new ArgumentException("The serialisation source cannot be null");
            }

            using (MemoryStream ms = new MemoryStream())
            {
                _formatter.Serialize(ms, source);
                return ms.ToArray();
            }
        }

        public T Deserialise(byte[] source)
        {
            if (source == null)
            {
                throw new ArgumentException("The deserialisation source cannot be null");
            }

            using (MemoryStream memStream = new MemoryStream())
            {
                memStream.Write(source, 0, source.Length);
                memStream.Seek(0, SeekOrigin.Begin);
                return (T)_formatter.Deserialize(memStream);
            }
        }
    }
}
