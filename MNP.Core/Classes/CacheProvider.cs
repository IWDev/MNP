using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MNP.Core
{
    /// <summary>
    /// Provides a base class for a subscribable local cache
    /// </summary>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TValue">The value type</typeparam>
    [Serializable]
    public abstract class CacheProvider<TKey, TValue> : IObservable<TValue>, IDisposable where TKey : class
    {
        // Maintains a list of subscribers from which we can notify about changes to the cache
        private readonly List<IObserver<TValue>> _subscribers = new List<IObserver<TValue>>(3);

        /// <summary>
        /// Write to the local cache and notify subscribers that we have a new entry. When overriding this method, ensure NotifySubscribers is called when addToLocalCacheOnly is false.
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="data">The value</param>
        /// <param name="addToLocalCacheOnly">Determins whether to add to the local cache only</param>
        public abstract void Write(TKey key, TValue data, bool addToLocalCacheOnly = false);

        /// <summary>
        /// Get/Set a specific entry in the cache
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>The value as type Y if exists. Else a KeyNotFoundException should be thrown.</returns>
        public abstract TValue this[TKey key] { get; set; }

        /// <summary>
        /// Determins whether or not a key exists
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>A boolean determining whether the key exists or not</returns>
        public abstract bool Contains(TKey key);

        /// <summary>
        /// Used to clean up resources
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Removes entries from the cache on a given criteria
        /// </summary>
        /// <param name="criteria"></param>
        /// <param name="notifySubscribers"></param>
        public abstract void Remove(TKey criteria, bool notifySubscribers);

        /// <summary>
        /// Notifies subscribers of a new entry of a task. Only parent classes can call this method.
        /// </summary>
        /// <param name="value">The entry that was added to the cache</param>
        protected Task NotifySubscribers(TValue value)
        {
            // Potentially a blocking operation, so return a new running task
            // Task.Run(someAction) is the same as Task.Factory.StartNew(someAction, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
            // source: http://blogs.msdn.com/b/pfxteam/archive/2011/10/24/10229468.aspx
            return Task.Factory.StartNew(() =>
                {
                    // we need to make sure that we are locking the list (thread safety)
                    lock (_subscribers)
                    {
                        foreach (IObserver<TValue> ob in _subscribers)
                        {
                            ob.OnNext(value);
                        }
                    }
                }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
        }

        /// <summary>
        /// Enables subscribtions to the cache
        /// </summary>
        /// <param name="observer"></param>
        /// <returns></returns>
        public IDisposable Subscribe(IObserver<TValue> observer)
        {
            // we need to make sure that we are locking the list (thread safety)
            lock (_subscribers)
            {
                _subscribers.Add(observer);
            }

            // return a new disposable which will clean up the resources when finished.
            return new Disposable(() => Unsubscribe(observer));
        }

        /// <summary>
        /// Unsubscribe a specific observer.
        /// </summary>
        /// <param name="observer"></param>
        private void Unsubscribe(IObserver<TValue> observer)
        {
            // we need to make sure that we are locking the list (thread safety)
            lock (_subscribers)
            {
                // sanity check - make sure they exist first
                if (_subscribers.Contains(observer))
                {
                    // tell the observer that we have finished
                    observer.OnCompleted();
                    // remove the observer from the list
                    _subscribers.Remove(observer);
                }
            }
        }

        /// <summary>
        /// Clears the subscribers ready for transportation across the interwebs.
        /// </summary>
        public void ClearSubscribersNoNotify()
        {
            lock (_subscribers)
            {
                _subscribers.Clear();
            }
        }

        /// <summary>
        /// Clears all the subscribers from the provider
        /// </summary>
        protected void ClearSubscribers()
        {
            // Trigger the OnCompleted event for each subscriber
            foreach (IObserver<TValue> observer in _subscribers)
            {
                observer.OnCompleted();
            }

            // release the reference to the subscribers
            _subscribers.Clear();
        }
    }
}
