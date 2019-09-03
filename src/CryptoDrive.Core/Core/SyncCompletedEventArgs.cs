using System;

namespace CryptoDrive.Core
{
    public class SyncCompletedEventArgs : EventArgs
    {
        public SyncCompletedEventArgs(int syncId, Exception exception)
        {
            this.SyncId = syncId;
            this.Exception = exception;
        }

        public int SyncId { get; }
        public Exception Exception { get; }
    }
}
