using System.Threading.Tasks;

namespace MNP.Core
{
    public interface INodeTask
    {
        Task<byte[]> Execute(byte[] data);
    }
}
