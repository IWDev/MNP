using MNP.Core.Enums;
using System;
using System.Threading.Tasks;

namespace MNP.Core
{
    /// <summary>
    /// Allows a class to be prioritised for use in an ObservablePriorityQueue
    /// </summary>
    public interface IPrioritised
    {
        /// <summary>
        /// The priority the class should take
        /// </summary>
        QueuePriority Priority { get; }

        /// <summary>
        /// The time of the class creation
        /// </summary>
        DateTime TimeStamp { get; }

        /// <summary>
        /// The state of the currently executing task
        /// </summary>
        QueuedProcessState State { get; set; }

        /// <summary>
        /// Used for anything that needs to be stored by the process, for example, the tasks unique identifer
        /// </summary>
        String Tag { get; }
    }
}
