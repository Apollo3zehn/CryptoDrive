using CryptoDrive.Core;
using Dropbox.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDrive.Drives
{
    public class DropboxProxy : IDriveProxy
    {
        #region Events

        public event EventHandler<DriveChangedNotification> FolderChanged;

        #endregion

        #region Fields

        private DropboxClient _client;

        #endregion

        #region Constructors

        public DropboxProxy(DropboxClient client)
        {
            _client = client;

            this.Name = "Dropbox";
        }

        #endregion

        #region Properties

        public string Name { get; }

        #endregion

        #region Navigation

        public async Task<List<CryptoDriveItem>> GetFolderContentAsync(CryptoDriveItem driveItem)
        {
            var path = driveItem.GetItemPath();

            if (path == "/")
                path = string.Empty;

            var items = await _client.Files.ListFolderAsync(path);

            var cryptoDriveItems = items.Entries.Select(item =>
            {
                var path = item.PathDisplay.Substring(0, item.PathDisplay.Length - item.Name.Length - 1);

                if (string.IsNullOrWhiteSpace(path))
                    path = "/";

                if (item.IsFolder)
                {
                    var folder = item.AsFolder;
                    return new CryptoDriveItem(folder.Name, path, DriveItemType.Folder);
                }
                else if (item.IsFile)
                {
                    var file = item.AsFile;
                    return new CryptoDriveItem(file.Name, path, DriveItemType.File);
                }
                else
                {
                    throw new NotSupportedException();
                }
            }).ToList();

            return cryptoDriveItems;
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
