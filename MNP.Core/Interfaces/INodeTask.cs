using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MNP.Core
{
    public interface INodeTask
    {
        Task<byte[]> Execute(byte[] data);
    }
}
