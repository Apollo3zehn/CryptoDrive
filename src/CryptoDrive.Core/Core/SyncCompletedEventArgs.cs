using System;

namespace CryptoDrive.Core
{
    public class SyncCompletedEventArgs : EventArgs
    {
        public SyncCompletedEventArgs(long syncId, Exception exception)
        {
            this.SyncId = syncId;
            this.Exception = exception;
        }

        public long SyncId { get; }
        public Exception Exception { get; }
    }
}
