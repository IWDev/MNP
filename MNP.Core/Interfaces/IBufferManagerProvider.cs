using System;
using System.Net.Sockets;

namespace MNP.Core
{
    /// <summary>
    /// Provides the methods needed to create a BufferManger
    /// </summary>
    public interface IBufferManagerProvider
    {
        /// <summary>
        /// Returns a Buffer from a ManagedResource
        /// </summary>
        /// <returns>A byte[] to be used as a buffer</returns>
        byte[] TakeNextBuffer();

        /// <summary>
        /// Inserts a buffer instance to a ManagedResource
        /// </summary>
        /// <param name="buffer">A byte[] that is used as a buffer</param>
        void InsertBuffer(byte[] buffer);

        /// <summary>
        /// Returns a SocketAsyncEventArgs instance from a ManagedResource
        /// </summary>
        /// <returns>An instance of SocketAsyncEventArgs</returns>
        SocketAsyncEventArgs TakeNextSocketAsyncEventArgs();

        /// <summary>
        /// Inserts a SocketAsyncEventArgs instance to a ManagedResource
        /// </summary>
        /// <param name="args">A SocketAsyncEventArgs to insert to the ManagedResource</param>
        void InsertSocketAsyncEventArgs(SocketAsyncEventArgs args);

        /// <summary>
        /// The defined limit of the ManagedResource
        /// </summary>
        Int32 Limit { get; }
    }
}
