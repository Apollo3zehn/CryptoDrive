namespace CryptoDrive.Core
{
    public class DriveChangedNotification
    {
        public DriveChangedNotification(string folderPath, SyncScope syncScope)
        {
            this.FolderPath = folderPath;
            this.SyncScope = syncScope;
        }

        public string FolderPath { get; }

        public SyncScope SyncScope { get; }
    }
}
