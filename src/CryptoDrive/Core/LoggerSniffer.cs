using Microsoft.Extensions.Logging;
using System;

namespace CryptoDrive.Core
{
    public class LoggerSniffer<T> : ILogger<T>
    {
        #region Events

        public event EventHandler<string> OnMessageLogged;

        #endregion

        #region Fields

        private ILogger<T> _logger;

        #endregion

        #region Constructors

        public LoggerSniffer(ILogger<T> logger)
        {
            _logger = logger;
        }

        #endregion

        #region Methods

        public IDisposable BeginScope<TState>(TState state)
        {
            return _logger.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _logger.IsEnabled(logLevel);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            _logger.Log(logLevel, eventId, state, exception, formatter);
            this.OnMessageLogged?.Invoke(this, formatter(state, exception));
        }

        #endregion
    }
}
