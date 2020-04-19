using System;
using System.Collections.Generic;

namespace CryptoDrive.Core
{
    public class DriveChangedEventArgs : EventArgs
    {
        public DriveChangedEventArgs(List<DriveChangedNotification> driveChangedNotifications)
        {
            this.DriveChangedNotifications = driveChangedNotifications;
        }

        public List<DriveChangedNotification> DriveChangedNotifications { get; }
    }
}
