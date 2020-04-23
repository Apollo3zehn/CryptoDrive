using CryptoDrive.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDrive.Drives
{
    public interface IDriveProxy : IDisposable
    {
        #region Events

        public event EventHandler<DriveChangedNotification> FolderChanged;

        #endregion

        #region Properties

        string Name { get; }

        #endregion

        #region Navigation

        Task<List<CryptoDriveItem>> GetFolderContentAsync(CryptoDriveItem driveItem);

        #endregion

        #region Change Tracking

        Task ProcessDelta(Func<List<CryptoDriveItem>, Task> action,
                          string folderPath,
                          CryptoDriveContext context,
                          DriveChangedType changeType,
                          CancellationToken cancellationToken);

        #endregion

        #region CRUD

        Task<CryptoDriveItem> CreateOrUpdateAsync(CryptoDriveItem driveItem, Stream content);
        Task<CryptoDriveItem> MoveAsync(CryptoDriveItem oldDriveItem, CryptoDriveItem newDriveItem);
        Task DeleteAsync(CryptoDriveItem driveItem);

        #endregion

        #region File Info

        Task<Stream> GetFileContentAsync(CryptoDriveItem driveItem);
        Task<bool> ExistsAsync(CryptoDriveItem driveItem);

        #endregion
    }
}