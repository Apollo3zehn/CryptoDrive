using CryptoDrive.Core;
using CryptoDrive.Cryptography;
using CryptoDrive.Drives;
using CryptoDrive.Extensions;
using CryptoDrive.Shared;
using Microsoft.Graph;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoDrive.Pages
{
    public partial class Restore
    {
        #region Fields

        private FileExplorer _fileExplorer;

        #endregion

        #region Commands

        public void OnKeyChanged()
        {

        }

        #endregion

        #region Methods

        private async Task StartAsync()
        {
            var settings = this.AppState.RestoreSettings;
            var drive = _fileExplorer.Drive;
            var sourceFolder = _fileExplorer.NavigationHierarchy.Last();
            var targetFolder = settings.RestoreFolder;
            var cryptonizer = new Cryptonizer(settings.RestoreKey);

            System.IO.Directory.CreateDirectory(targetFolder);
            await this.RestoreHierarchyAsync(sourceFolder, targetFolder, cryptonizer, drive);
        }

        private async Task RestoreHierarchyAsync(DriveItem sourceFolder, string targetFolder, Cryptonizer cryptonizer, IDriveProxy drive)
        {
            var driveItems = await drive.GetFolderContentAsync(sourceFolder);

            foreach (var driveItem in driveItems)
            {
                var itemPath = Path.Combine(targetFolder, driveItem.Name);

                switch (driveItem.Type())
                {
                    case DriveItemType.File:

                        using (var encryptedStream = await drive.GetFileContentAsync(driveItem))
                        using (var decryptedStream = cryptonizer.CreateDecryptStream(encryptedStream))
                        using (var fileStream = System.IO.File.Create(itemPath))
                        {
                            await decryptedStream.CopyToAsync(fileStream);
                        }


                        break;

                    case DriveItemType.Folder:

                        System.IO.Directory.CreateDirectory(itemPath);
                        await this.RestoreHierarchyAsync(driveItem, itemPath, cryptonizer, drive);

                        break;

                    default:
                        throw new NotSupportedException();
                }
            }
        }

        #endregion
    }
}
