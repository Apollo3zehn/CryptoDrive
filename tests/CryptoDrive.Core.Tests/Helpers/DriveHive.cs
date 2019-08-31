using System;

namespace CryptoDrive.Core.Tests
{
    public class DriveHive : IDisposable
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

        public void Dispose()
        {
            this.LocalDrive.Dispose();
            this.RemoteDrive.Dispose();
        }
    }
}
