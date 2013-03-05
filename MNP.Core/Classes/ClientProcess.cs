using System;
using System.Net;
using MNP.Core.Enums;

namespace MNP.Core.Classes
{
    [Serializable]
    public class ClientProcess : IPrioritised
    {
        /// <summary>
        /// The priority of the item
        /// </summary>
        public QueuePriority Priority { get; private set; }

        /// <summary>
        /// All times are UTC from when the object was created.
        /// </summary>
        public DateTime TimeStamp { get; private set; }

        public ClientProcess()
        {
            TimeStamp = DateTime.UtcNow;
            Priority = QueuePriority.Normal;
        }

        public string Tag { get; set; }

        public QueuedProcessState State { get; set; }

        public byte[] Data { get; set; }
        public bool LocalOnly { get; set; }
        public IPAddress Source { get; set; }
    }
}
