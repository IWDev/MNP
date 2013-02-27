using System;
using System.Collections.Generic;
using System.Threading;

namespace MNP.Core
{
    /// <summary>
    /// Creates a managed Stack that makes a program wait when the resources are depleated
    /// </summary>
    /// <typeparam name="T">The type of Stack to create</typeparam>
    public sealed class ManagedStack<T> : IManagedResource<T>
    {
        #region "Constructors"
        private ManagedStack() { }
        public ManagedStack(Int32 capacity) : this(capacity, false, null) { }
        public ManagedStack(Int32 capacity, bool fillStack) : this(capacity, fillStack, null) { }
        public ManagedStack(Int32 capacity, bool fillStack, Stack<T> stack)
        {
            this.Capacity = capacity;
            _stack = (stack == null) ? new Stack<T>(capacity) : stack;
            _restrictor = new SemaphoreSlim((fillStack) ? this.Capacity : 0, this.Capacity);

            if (fillStack)
            {
                // Setup the stack with default values
                for (Int32 i = 0; i < this.Capacity; i++)
                {
                    Insert(default(T));
                }
            }
        }
        #endregion

        /// <summary>
        /// Gets the defined over all Stack Capacity
        /// </summary>
        public Int32 Capacity { get; private set; }

        // The stack to hold the items
        private Stack<T> _stack;

        // The SemaphoreSlim to restrict access to the stack
        private SemaphoreSlim _restrictor;

        /// <summary>
        /// Take the next resource available from the stack. This is a blocking operation if capacity is reached.
        /// </summary>
        /// <returns>The next resource available</returns>
        public T TakeNext()
        {
            // Sanity Check
            if (_stack == null)
            {
                throw new InvalidOperationException("The stack cannot be null");
            }

            // make us wait if necessary
            _restrictor.Wait();

            lock (_stack)
            {
                if (_stack.Count > 0)
                {
                    return _stack.Pop();
                }
                throw new Exception("There has been a Semaphore/Stack offset");
            }
        }

        /// <summary>
        /// Adds an item to the stack. This will release other threads if they are blocked
        /// </summary>
        /// <param name="item"></param>
        public void Insert(T item)
        {
            // Sanity Check
            if (_stack == null)
            {
                throw new InvalidOperationException("The stack cannot be null");
            }

            // Sanity Check
            if (item == null)
            {
                throw new ArgumentException("The item cannot be null");
            }

            lock (_stack)
            {
                _stack.Push(item);
                _restrictor.Release();
            }
        }
    }
}
