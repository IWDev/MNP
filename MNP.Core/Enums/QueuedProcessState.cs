using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MNP.Core.Enums
{
    public enum QueuedProcessState
    {
        None = 0,
        Runnable = 1,
        Running = 2,
        Finished = 3
    }
}
