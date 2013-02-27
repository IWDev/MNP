using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNP.Core
{
    /// <summary>
    /// Creates a managed Queue that makes a program wait when the resources are depleated
    /// </summary>
    /// <typeparam name="T">The type of Queue to create</typeparam>
    public sealed class ManagedQueue<T> : IManagedResource<T>
    {
        #region "Constructors"
        private ManagedQueue() { }
        public ManagedQueue(Int32 capacity) : this(capacity, false, null) { }
        public ManagedQueue(Int32 capacity, bool fillQueue) : this(capacity, fillQueue, null) { }
        public ManagedQueue(Int32 capacity, bool fillQueue, Queue<T> queue)
        {
            this.Capacity = capacity;
            _queue = (queue == null) ? new Queue<T>(capacity) : queue;
            _restrictor = new SemaphoreSlim((fillQueue) ? this.Capacity : 0, this.Capacity);

            if (fillQueue)
            {
                // Setup the queue with default values
                for (Int32 i = 0; i < this.Capacity; i++)
                {
                    Insert(default(T));
                }
            }
        }
        #endregion

        /// <summary>
        /// Gets the defined over all queue Capacity
        /// </summary>
        public Int32 Capacity { get; private set; }

        // The queue to hold the items
        private Queue<T> _queue;

        // The SemaphoreSlim to restrict access to the queue
        private SemaphoreSlim _restrictor;

        /// <summary>
        /// Take the next resource available from the queue. This is a blocking operation if capacity is reached.
        /// </summary>
        /// <returns>The next resource available</returns>
        public T TakeNext()
        {
            // Sanity Check
            if (_queue == null)
            {
                throw new InvalidOperationException("The queue cannot be null");
            }

            // make us wait if necessary
            _restrictor.Wait();

            lock (_queue)
            {
                if (_queue.Count > 0)
                {
                    return _queue.Dequeue();
                }
                throw new Exception("There has been a Semaphore/queue offset");
            }
        }

        /// <summary>
        /// Adds an item to the queue. This will release other threads if they are blocked
        /// </summary>
        /// <param name="item"></param>
        public void Insert(T item)
        {
            // Sanity Check
            if (_queue == null)
            {
                throw new InvalidOperationException("The queue cannot be null");
            }

            // Sanity Check
            if (item == null)
            {
                throw new ArgumentException("The item cannot be null");
            }

            lock (_queue)
            {
                _queue.Enqueue(item);
                _restrictor.Release();
            }
        }
    }
}
