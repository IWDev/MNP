using System;
using MNP.Core.Enums;

namespace MNP.Core
{
    /// <summary>
    /// Provides the methods necessary for the Queue
    /// </summary>
    /// <typeparam name="T">The type the Queue should take</typeparam>
    public interface IObservableQueue<T>
    {
        /// <summary>
        /// Add an element to the Queue
        /// </summary>
        /// <param name="value">The item to add</param>
        /// <param name="notifySubscribers"></param>
        void Enqueue(T value, bool notifySubscribers);

        /// <summary>
        /// Remove the first element from the Queue
        /// </summary>
        /// <returns></returns>
        T Dequeue();

        /// <summary>
        /// Get the first item in the Queue or the default value of T
        /// </summary>
        T PeekOrDefault();

        /// <summary>
        /// The current number of items in the Queue
        /// </summary>
        Int32 Count { get; }

        void ChangeState(string id, QueuedProcessState newState, bool localOnly);

        void Remove(Predicate<T> criteria);

        IDisposable Subscribe(IObserver<T> observer);

        void Unsubscribe(IObserver<T> observer);
    }
}
