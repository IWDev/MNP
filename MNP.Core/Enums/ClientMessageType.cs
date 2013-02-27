using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MNP.Core.Enums
{
    public enum ClientMessageType
    {
        None = 0, 
        NewTask = 1, 
        FetchResult = 2,
        TimeoutPrevention = 3
    }
}
