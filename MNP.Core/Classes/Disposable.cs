using System;

namespace MNP.Core
{
    /// <summary>
    /// Allows a specific, parameterless action to be run when an disposed.
    /// </summary>
    public sealed class Disposable : IDisposable
    {
        readonly Action _action;

        /// <summary>
        /// Creates a new instance of Disposable
        /// </summary>
        /// <param name="action">The action to be run when disposed</param>
        public Disposable(Action action)
        {
            _action = action;
        }

        /// <summary>
        /// Trigger the stored action
        /// </summary>
        public void Dispose()
        {
            // Make sure that the action is actually something before trying to invoke it
            if (_action != null)
            {
                _action();
            }
        }
    }
}
