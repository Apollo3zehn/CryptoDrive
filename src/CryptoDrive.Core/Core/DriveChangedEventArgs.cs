using System;
using System.Collections.Generic;

namespace CryptoDrive.Core
{
    public class DriveChangedEventArgs : EventArgs
    {
        public DriveChangedEventArgs(List<DriveChangedNotification> changeNotifications)
        {
            this.ChangeNotifications = changeNotifications;
        }

        public List<DriveChangedNotification> ChangeNotifications { get; }
    }
}
