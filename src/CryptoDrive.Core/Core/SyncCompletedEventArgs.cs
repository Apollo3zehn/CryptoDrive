using System;

namespace CryptoDrive.Core
{
    public class SyncCompletedEventArgs : EventArgs
    {
        public SyncCompletedEventArgs(int syncId)
        {
            this.SyncId = syncId;
        }

        public int SyncId { get; }
    }
}
