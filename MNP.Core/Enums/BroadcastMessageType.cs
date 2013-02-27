using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MNP.Core
{
    public enum BroadcastMessageType
    {
        None = 0, 
        Startup = 1,
        Shutdown = 2,
        MasterNodeChallenge = 3,
        MasterNodeResponse = 4
    }
}
