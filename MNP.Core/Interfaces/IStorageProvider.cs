using System.IO;

namespace MNP.Core
{
    public interface IStorageProvider
    {
        // For writing objects
        void Write(string key, byte[] data);

        // For the case of a log writter
        void Write(string key, string str);

        // For reading back from the data store
        Stream Read(string key);
    }
}
