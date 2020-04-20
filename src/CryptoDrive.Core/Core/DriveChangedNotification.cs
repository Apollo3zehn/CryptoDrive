namespace CryptoDrive.Core
{
    public struct DriveChangedNotification
    {
        #region Constructors

        public DriveChangedNotification(string folderPath, DriveChangedType changeType)
        {
            this.FolderPath = folderPath;
            this.ChangeType = changeType;
        }

        #endregion

        #region Properties

        public string FolderPath { get; }

        public DriveChangedType ChangeType { get; }

        #endregion
    }
}
