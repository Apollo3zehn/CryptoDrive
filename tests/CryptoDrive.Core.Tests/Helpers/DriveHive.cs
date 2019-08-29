namespace CryptoDrive.Core.Tests
{
    public class DriveHive
    {
        public DriveHive(IDriveProxy remoteDrive, IDriveProxy localDrive, string remoteDrivePath, string localDrivePath)
        {
            this.RemoteDrive = remoteDrive;
            this.LocalDrive = localDrive;
            this.RemoteDrivePath = remoteDrivePath;
            this.LocalDrivePath = localDrivePath;
        }

        public IDriveProxy RemoteDrive { get; }
        public IDriveProxy LocalDrive { get; }
        public string RemoteDrivePath { get; }
        public string LocalDrivePath { get; }
    }
}
