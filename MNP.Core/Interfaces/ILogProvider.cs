using System;

namespace MNP.Core
{
    /// <summary>
    /// Provides the methods needed to create a LogProvider
    /// </summary>
    public interface ILogProvider
    {
        /// <summary>
        /// Classes using this property should provide a private set operation.
        /// </summary>
        IStorageProvider StorageProvider { get; }

        /// <summary>
        /// Writes an entry to the log.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="source">The function name that Log method is called from. Best practise is to include class name.</param>
        /// <param name="loggingLevel">The level of logging required before the log entry is written.</param>
        void Log(String message, String source, LogLevel loggingLevel);
    }
}
