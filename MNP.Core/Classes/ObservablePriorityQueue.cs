using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MNP.Core.Enums;

namespace MNP.Core
{
    /// <summary>
    /// A queue that supports IObservable and prioritisation.
    /// </summary>
    /// <typeparam name="T">The IPrioritised type that the queue should form around</typeparam>
    [Serializable]
    public sealed class ObservablePriorityQueue<T> : IObservableQueue<T>, IObservable<T> where T : class, IPrioritised
    {
        #region "IObservable<T> Implementation"
        // A list of the subscribers for the IObservable implementation
        readonly List<IObserver<T>> _subscribers = new List<IObserver<T>>(10);

        #region "Interface specific"
        /// <summary>
        /// Allows a subscription to the queue
        /// </summary>
        /// <param name="observer">The observer to add</param>
        /// <returns>An Disposable object that triggers the Unsubscribe method</returns>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            // sanity check
            if (observer == null)
            {
                throw new ArgumentNullException("observer");
            }
            // Lock the resource before reading/writing (thread-safety)
            lock (_subscribers)
            {
                if (!_subscribers.Contains(observer))
                {
                    _subscribers.Add(observer);
                }
            }
            // Return the unsubscribe method
            return new Disposable(() => Unsubscribe(observer));
        }
        #endregion

        /// <summary>
        /// Unsubscribes an observer from the queue
        /// </summary>
        /// <param name="observer">The observer to unsubscribe</param>
        public void Unsubscribe(IObserver<T> observer)
        {
            // Sanity check
            if (observer == null)
            {
                throw new ArgumentNullException("observer");
            }

            // make sure the observer knows that they should finish with the subscription
            observer.OnCompleted();

            // Lock the resource before reading/writing (thread-safety)
            lock (_subscribers)
            {
                if (_subscribers.Contains(observer))
                {
                    // remove the entry, but don't dispose it just 
                    // in case they want to re-subscribe with the same observer later
                    _subscribers.Remove(observer);
                }
            }
        }
        #endregion

        #region "IQueue<T> Implementation"

        // Hold all of the data in a list, we can then use LINQ-to-Objects to pull the data we want in order
        readonly List<T> _data = new List<T>(100);

        /// <summary>
        /// Add a value to the queue
        /// </summary>
        /// <param name="value">The value to add to the queue</param>
        /// <param name="notifySubscribers"></param>
        public void Enqueue(T value, bool notifySubscribers)
        {
            // Sanity check
            if (value == null)
            {
                throw new ArgumentException("The item to be enqueued cannot be null");
            }

            // Lock the resource before reading/writing (thread-safety)
            lock (_data)
            {
                _data.Add(value);
            }

            if (notifySubscribers)
            {
                // now that the entry has been added, notify everyone
                Task.Run(() =>
                {
                    lock (_subscribers)
                    {
                        foreach (IObserver<T> subscriber in _subscribers)
                        {
                            subscriber.OnNext(value);
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Removes the first runnable element from the Queue
        /// </summary>
        /// <returns>The most important value in the Queue</returns>
        public T Dequeue()
        {
            // Lock the resource before reading/writing (thread-safety)
            lock (_data)
            {
                if (_data.Count > 0) // this must be inside the lock otherwise it might be modified by the time we read from the list
                {
                    var result = _data.Where(element => element.State == QueuedProcessState.Runnable).OrderByDescending(element => element.Priority).ThenBy(element => element.TimeStamp).First();
                    _data.Remove(result);
                    return result;
                }
            }
            throw new InvalidOperationException("There are no entries in the queue in order to dequeue");
        }

        /// <summary>
        /// Takes a look inside the list and returns the first runnable element in the Queue or its default value
        /// </summary>
        /// <returns></returns>
        public T PeekOrDefault()
        {
            // Lock the resource before reading/writing (thread-safety)
            lock (_data)
            {
                if (_data.Count > 0) // this must be inside the lock otherwise it might be modified by the time we read from the list
                {

                    return _data.Where(element => element.State == QueuedProcessState.Runnable).OrderByDescending(element => element.Priority).ThenBy(element => element.TimeStamp).First();
                }
            }
            return default(T);
        }

        /// <summary>
        /// Returns the amount of elements in the queue
        /// </summary>
        public Int32 Count
        {
            get
            {
                // Lock the resource before reading (thread-safety)
                lock (_data)
                {
                    return _data.Count;
                }
            }
        }
        #endregion

        public void Remove(Predicate<T> criteria)
        {
            _data.Remove(_data.Find(criteria));
        }

        public void ChangeState(string id, QueuedProcessState newState, bool localOnly)
        {
            lock (_data)
            {
                _data.First(x => x.Tag == id).State = newState;
            }

            if (!localOnly)
            {
                // TODO :: register the change with the other subscribers    
            }
        }

        /// <summary>
        /// Clears the subscribers ready for transportation across the interwebs.
        /// </summary>
        public void ClearSubscribers()
        {
            lock (_subscribers)
            {
                _subscribers.Clear();
            }
        }
    }
}
