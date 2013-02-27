using System;
using MNP.Core.Enums;

namespace MNP.Core.Classes
{
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

        ClientProcess()
        {
            this.TimeStamp = DateTime.UtcNow;
            this.Priority = QueuePriority.None;
        }

        public string Tag { get; set; }

        public QueuedProcessState State { get; set; }
    }
}
