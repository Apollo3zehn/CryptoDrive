using CryptoDrive.Core;
using Dropbox.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDrive.Drives
{
    public class GoogleDriveProxy : IDriveProxy
    {
        #region Events

        public event EventHandler<DriveChangedNotification> FolderChanged;

        #endregion

        #region Constructors

        public GoogleDriveProxy()
        {
            this.Name = "Google Drive";
        }

        #endregion

        #region Properties

        public string Name { get; }

        #endregion

        #region Navigation

        public Task<List<CryptoDriveItem>> GetFolderContentAsync(CryptoDriveItem driveItem)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Change Tracking

        public Task ProcessDelta(Func<List<CryptoDriveItem>, Task> action, string folderPath, CryptoDriveContext context, DriveChangedType changeType, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region CRUD

        public Task<CryptoDriveItem> CreateOrUpdateAsync(CryptoDriveItem driveItem, Stream content, CancellationToken cts)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAsync(CryptoDriveItem driveItem)
        {
            throw new NotImplementedException();
        }

        public Task<CryptoDriveItem> MoveAsync(CryptoDriveItem oldDriveItem, CryptoDriveItem newDriveItem)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region File Info

        public Task<Stream> GetFileContentAsync(CryptoDriveItem driveItem)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ExistsAsync(CryptoDriveItem driveItem)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            //
        }

        #endregion 
    }
}
