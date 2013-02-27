using System;
using System.Collections.Generic;

namespace MNP.Core
{
    /// <summary>
    /// A simple generic cache that supports subscribtions.
    /// </summary>
    /// <typeparam name="T">Must be a class.</typeparam>
    public class ObservableCache<T, Y> : IObservable<Y>, IDisposable where T : class
    {
        #region "Cache Implementation"

        /// <summary>
        /// The main object that holds the cache entries.
        /// </summary>
        Dictionary<T, Y> _cache = new Dictionary<T, Y>();

        /// <summary>
        /// Gets or sets an entry to the cache. Subscribers are notified if a new entry is added.
        /// </summary>
        /// <param name="entry">The name of the cache entry to retrieve.</param>
        /// <returns>A valid entry or null</returns>
        public Y this[T entry]
        {
            get
            {
                lock (_cache)
                {
                    if (!_cache.ContainsKey(entry))
                    {
                        throw new KeyNotFoundException();
                    }

                    return _cache[entry];
                }
            }
            set
            {
                // Explicitly call the second overload to prevent an extra call on the stack. I am that good to the CLR!
                AddNewEntry(entry, value, false);
            }
        }

        /// <summary>
        /// Adds a new entry to the cache and notifies subscribers.
        /// </summary>
        /// <param name="entry">The name of the cache entry.</param>
        /// <param name="value">The value to add to the cache (Of type T).</param>
        public void AddNewEntry(T entry, Y value)
        {
            AddNewEntry(entry, value, false);
        }

        /// <summary>
        /// Adds a new entry to the cache and notifies subscribers. Optionally overwritting an existing entry with the same name.
        /// </summary>
        /// <param name="entry">The name of the cache entry.</param>
        /// <param name="value">The value to add to the cache (Of type T).</param>
        /// <param name="overwriteExisting">Determines whether or not to overwrite the existing entry or not. This does not notify subscribers.</param>
        public void AddNewEntry(T entry, Y value, Boolean overwriteExisting)
        {
            lock (_cache)
            {
                if (_cache.ContainsKey(entry))
                {
                    if (!overwriteExisting)
                    {
                        throw new Exception("A cache entry with that name already exists. If you wish to override the entry, please set 'overwriteExisting' to 'true'.");
                    }
                    else
                    {

                        _cache[entry] = value;
                    }
                }
                else
                {
                    _cache.Add(entry, value);
                    lock (_subscribers)
                    {
                        // now that the entry has been added, notify everyone
                        foreach (IObserver<Y> subscriber in _subscribers)
                        {
                            subscriber.OnNext(value);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Removes the specified entry from the cache.
        /// </summary>
        /// <param name="entry">The name of the entry to remove.</param>
        public void RemoveEntry(T entry)
        {
            lock (_cache)
            {
                if (_cache.ContainsKey(entry))
                {
                    _cache.Remove(entry);
                }
            }
        }

        /// <summary>
        /// Clears the cache of all entries, without notification to subscribers.
        /// </summary>
        /// <param name="clearSubscribers">Determines whether to clear the subscribers as well.</param>
        public void Clear(Boolean clearSubscribers = false)
        {
            lock (_cache)
            {
                lock (_subscribers)
                {
                    if (clearSubscribers)
                    {
                        for (Int32 i = _subscribers.Count; i > 0; i--)
                        {
                            _subscribers[i].OnCompleted();
                            _subscribers.RemoveAt(i);
                        }
                    }
                }
                _cache.Clear();
            }
        }

        public bool Contains(T key)
        {
            lock (_cache)
            {
                return _cache.ContainsKey(key);
            }
        }

        #endregion

        #region "IDisposable Implementation"
        /// <summary>
        /// Provides cleanup of the class.
        /// </summary>
        public void Dispose()
        {
            // clear the cache before proceeding
            Clear(true);

            // now all of the references have been taken care of...
            lock (_subscribers)
            {
                _subscribers = null;
            }
        }
        #endregion

        #region "Subscription Management"
        #region "IObservable Implementation"

        /// <summary>
        /// The list of all the subscribers.
        /// </summary>
        List<IObserver<Y>> _subscribers = new List<IObserver<Y>>();

        /// <summary>
        /// Allows Observers to watch for new cache entries.
        /// </summary>
        /// <param name="observer">The observer to add.</param>
        /// <returns></returns>
        public IDisposable Subscribe(IObserver<Y> observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException("The observer cannot be null.");
            }

            lock (_subscribers)
            {
                if (!_subscribers.Contains(observer))
                {
                    _subscribers.Add(observer);
                }
            }

            return new Disposable(() =>
            {
                this.Unsubscribe(observer);
            });
        }
        #endregion

        /// <summary>
        /// Removes a subscribe from the chain.
        /// </summary>
        /// <param name="observer">The subscriber to remove.</param>
        public void Unsubscribe(IObserver<Y> observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException("The observer cannot be null.");
            }

            observer.OnCompleted();

            lock (_subscribers)
            {
                if (_subscribers.Contains(observer))
                {
                    // remove the entry from the cache, but don't dispose it just 
                    // in case they want to re-subscribe with the same observer later
                    _subscribers.Remove(observer);
                }
            }
        }
        #endregion
    }
}
