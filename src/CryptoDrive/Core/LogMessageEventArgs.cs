using Microsoft.Extensions.Logging;

namespace CryptoDrive.Core
{
    public class LogMessageEventArgs
    {
        #region Constructors

        public LogMessageEventArgs(LogLevel logLevel, string message)
        {
            this.LogLevel = logLevel;
            this.Message = message;
        }

        #endregion

        #region Properties

        public LogLevel LogLevel { get; }

        public string Message { get; }

        #endregion
    }
}
