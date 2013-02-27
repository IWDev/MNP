using MNP.Core;
using System;
using System.Diagnostics;

namespace MNP.Server.Providers
{
    /// <summary>
    /// Provides a basic implementation of a ILogProvider
    /// </summary>
    public sealed class DefaultLogProvider : ILogProvider
    {
        // The actual underlying storage provider
        private IStorageProvider _storageProvider;

        /// <summary>
        /// The level of logging to obtain
        /// </summary>
        public LogLevel LoggingLevel { get; private set; }

        #region "Constructors"
        public DefaultLogProvider() : this(LogLevel.None, null) { }

        public DefaultLogProvider(LogLevel logLevel) : this(logLevel, null) { }

        public DefaultLogProvider(LogLevel logLevel, IStorageProvider storage)
        {
            this.LoggingLevel = logLevel;
            _storageProvider = (storage == null) ? new DefaultStorageProvider() : storage;
        }
        #endregion

        /// <summary>
        /// The underlying storage provider.
        /// </summary>
        public IStorageProvider StorageProvide
        {
            get { return _storageProvider; }
            private set
            {
                if (value == null)
                {
                    throw new ArgumentException("StorageProvider cannot be null");
                }
                else
                {
                    _storageProvider = value;
                }
            }
        }
        
        /// <summary>
        /// Logs the message to the underlying storage provider
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="source">The source (EG. ClassName.MethodName</param>
        /// <param name="loggingLevel">The minimum level of logging needed</param>
        public void Log(string message, string source, LogLevel loggingLevel)
        {
            if (loggingLevel == LogLevel.None)
            {
                throw new NotSupportedException("Logging Level 'None' was not meant to be used in this way and thus unsupported.");
            }
            if (AllowedToLog(loggingLevel))
            {
                // this will do for now, add a proper implementation later.
                Debug.WriteLine("[{0} {1}] {2}", DateTime.Now.ToString(), source, message);
            }
        }
        
        /// <summary>
        /// Determines whether a message can be logged or not
        /// </summary>
        /// <param name="lvl"></param>
        /// <returns></returns>
        private bool AllowedToLog(LogLevel lvl)
        {
            if (this.LoggingLevel == LogLevel.None)
            {
                return false;
            }
            else if ((this.LoggingLevel == LogLevel.Minimal && lvl == LogLevel.Minimal) || (this.LoggingLevel == LogLevel.Verbose))
            {
                return true;   
            }
            return false;
        }
    }
}
