using MNP.Core;
using System.Collections.Generic;
using System;

namespace MNP.Server.Providers
{
    /// <summary>
    /// Provides a basic implementation of a caching system
    /// </summary>
    /// <typeparam name="T">The key</typeparam>
    /// <typeparam name="Y">The value</typeparam>
    [Serializable]
    public sealed class DefaultCache<T, Y> : CacheProvider<T, Y> where T : class
    {
        // hold the data here
        private readonly ObservableCache<T, Y> _cache = new ObservableCache<T, Y>();

        /// <summary>
        /// Indexer property
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <returns>The value if found by the key, else a KeyNotFoundException is thrown</returns>
        public override Y this[T key]
        {
            get
            {
                if (_cache.Contains(key))
                {
                    return _cache[key];
                }
                throw new KeyNotFoundException();
            }
            set
            {
                _cache.AddNewEntry(key, value);
            }
        }

        /// <summary>
        /// Determines whether or not a specified key exists
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>A bool stating whether the key exists or not</returns>
        public override bool Contains(T key)
        {
            return _cache.Contains(key);
        }

        /// <summary>
        /// Adds an entry to the Cache
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="data">The data to store in the cache</param>
        /// <param name="addToLocalCacheOnly">Whether to add the key to the local cache only or notify subscribers.</param>
        public override void Write(T key, Y data, bool addToLocalCacheOnly = false)
        {
            // add the entry to our cache regardless
            _cache.AddNewEntry(key, data);

            // if we are only adding to the local cache, we do not need to notify subscribers
            if (!addToLocalCacheOnly)
            {
                this.NotifySubscribers(data);
            }
        }

        public override void Remove(T criteria, bool notifySubscribers)
        {
            _cache.RemoveEntry(criteria);
        }

        /// <summary>
        /// Resource cleanup
        /// </summary>
        public override void Dispose()
        {
            // clear our cache, starting with the subscribers
            _cache.Clear(true); // true clears the subscribers, false does not
            _cache.Dispose();
        }
    }
}
