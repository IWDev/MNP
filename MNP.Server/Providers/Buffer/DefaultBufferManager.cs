using MNP.Core;
using System;
using System.Net.Sockets;

namespace MNP.Server.Providers
{
    /// <summary>
    /// Provides a way of holding SocketAsyncEventArgs and byte[] buffers in order to help prevent memory fragementation
    /// </summary>
    public sealed class DefaultBufferManager : IBufferManagerProvider
    {
        #region "Public Properties"
        /// <summary>
        /// The maximum capacity of the IManagedResources
        /// </summary>
        public Int32 Limit { get; private set; }
        /// <summary>
        /// The size of the byte[] buffers
        /// </summary>
        public Int32 BufferSize { get; private set; }
        #endregion

        #region "Private Members"
        /// <summary>
        /// Holds the SocketAsyncEventArgs
        /// </summary>
        private IManagedResource<SocketAsyncEventArgs> _sockets;
        /// <summary>
        /// Holds the byte[] buffers
        /// </summary>
        private IManagedResource<byte[]> _buffers;
        #endregion

        #region "Constructors"
        public DefaultBufferManager() : this(100, 2048, null, null) { }
        public DefaultBufferManager(Int32 limit, Int32 bufferSize, IManagedResource<SocketAsyncEventArgs> sockets, IManagedResource<byte[]> buffers)
        {
            this.Limit = limit;
            this.BufferSize = bufferSize;

            // create the stacks
            _sockets = (sockets == null) ? new ManagedStack<SocketAsyncEventArgs>(limit, false) : sockets;
            _buffers = (buffers == null) ? new ManagedStack<byte[]>(limit, false) : buffers;

            // fill the resources ready
            for (Int32 i = 0; i < limit; i++)
            {
                _buffers.Insert(new byte[bufferSize]);
                _sockets.Insert(new SocketAsyncEventArgs());
            }
        }
        #endregion

        #region "IBufferManagerProvider Implementation"
        /// <summary>
        /// Inserts a byte[] into the pool
        /// </summary>
        /// <param name="buffer">The array to insert</param>
        public void InsertBuffer(byte[] buffer)
        {
            // sanity check
            if (buffer != null)
            {
                // Make sure the array is the correct size
                if (buffer.Length != this.BufferSize)
                {
                    Array.Resize(ref buffer, this.BufferSize);
                }
                // clear the array
                Array.Clear(buffer, 0, buffer.Length);
                // give the array back to the pool
                _buffers.Insert(buffer);
            }
        }

        /// <summary>
        /// Takes the next byte[] from the pool. This operation may block until one is free in the pool.
        /// </summary>
        /// <returns>The next availble byte[]</returns>
        public byte[] TakeNextBuffer()
        {
            // Take the next one from the ManagedResource
            return _buffers.TakeNext();
        }

        /// <summary>
        /// Inserts a SocketAsyncEventArgs in to the pool
        /// </summary>
        /// <param name="args">The SocketAsyncEventArgs to insert</param>
        public void InsertSocketAsyncEventArgs(SocketAsyncEventArgs args)
        {
            // sanity check
            if (args == null)
            {
                throw new ArgumentException("The SocketAsyncEventArgs cannot be null");
            }
            else
            {
                // Clear the args
                args.UserToken = null;
                args.AcceptSocket = null;
                // give the args back to the pool
                _sockets.Insert(args);
            }
        }

        /// <summary>
        /// Takes the next SocketAsyncEventArgs from the pool. This operation may block until one is free in the pool.
        /// </summary>
        /// <returns>A fresh instance of SocketAsyncEventArgs from the pool</returns>
        public SocketAsyncEventArgs TakeNextSocketAsyncEventArgs()
        {
            // Take the next one from the ManagedResource
            return _sockets.TakeNext();
        }
        #endregion
    }
}
