using System;
namespace MNP.Core
{
    /// <summary>
    /// Provides the methods needed to a ManagedResource. Resources should be restricted via a Semaphore or similar.
    /// </summary>
    /// <typeparam name="T">The type the MangedResource should take</typeparam>
    public interface IManagedResource<T>
    {
        /// <summary>
        /// The total capacity of the ManagedResource
        /// </summary>
        int Capacity { get; }

        /// <summary>
        /// Inserts an item into the ManagedResource's Collection. This operation should release a pending blocked operation.
        /// </summary>
        /// <param name="item">The item to return to the collection</param>
        void Insert(T item);

        /// <summary>
        /// Takes the next item from the ManagedResource's Collection. This operation can block.
        /// </summary>
        T TakeNext();
    }
}
