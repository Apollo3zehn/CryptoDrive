using CryptoDrive.Core;
using System;
using System.IO;

namespace CryptoDrive.Extensions
{
    public static class CryptoDriveItemExtensions
    {
        // from remote state to drive item and vice versa
        public static RemoteState ToRemoteState(this CryptoDriveItem driveItem)
        {
            var type = driveItem.Type;

            return new RemoteState()
            {
                Path = driveItem.Path,
                Id = driveItem.Id,
                Name = driveItem.Name,
                LastModified = driveItem.LastModified,
                Size = driveItem.Size,
                Type = type
            };
        }

        public static CryptoDriveItem ToDriveItem(this RemoteState remoteState, bool deleted = false)
        {
            return new CryptoDriveItem(remoteState.Id, remoteState.Name, remoteState.Path, remoteState.Type)
            {
                IsDeleted = deleted,
                LastModified = remoteState.LastModified,
                Size = remoteState.Size
            };
        }

        // from x to drive item
        public static CryptoDriveItem ToDriveItem(this string itemPath, DriveItemType type)
        {
            if (itemPath == "/")
                return itemPath.ToSpecialDriveItem();

            var itemName = Path.GetFileName(itemPath);
            var folderPath = Path.GetDirectoryName(itemPath).NormalizeSlashes();
            var driveItem = new CryptoDriveItem(itemName, folderPath, type);

            return driveItem;
        }

        private static CryptoDriveItem ToSpecialDriveItem(this string itemPath)
        {
            var itemName = "";
            var folderPath = itemPath;
            var driveItem = new CryptoDriveItem(itemName, folderPath, DriveItemType.Folder);

            return driveItem;
        }

        public static CryptoDriveItem ToDriveItem(this DirectoryInfo folderInfo, string basePath)
        {
            var folderName = folderInfo.Name;
            var folderPath = folderInfo.Parent.FullName.Substring(basePath.Length).NormalizeSlashes();
            var lastModified = folderInfo.LastWriteTimeUtc;

            // remove millisecond part
            lastModified = new DateTime(lastModified.Year,
                                        lastModified.Month,
                                        lastModified.Day,
                                        lastModified.Hour,
                                        lastModified.Minute,
                                        lastModified.Second);

            var driveItem = new CryptoDriveItem(folderName, folderPath, DriveItemType.Folder)
            {
                LastModified = lastModified
            };

            return driveItem;
        }

        public static CryptoDriveItem ToDriveItem(this FileInfo fileInfo, string basePath)
        {
            var fileName = fileInfo.Name;
            var folderPath = fileInfo.DirectoryName.Substring(basePath.Length).NormalizeSlashes();
            var lastModified = fileInfo.LastWriteTimeUtc;

            // remove millisecond part
            lastModified = new DateTime(lastModified.Year,
                                        lastModified.Month,
                                        lastModified.Day,
                                        lastModified.Hour,
                                        lastModified.Minute,
                                        lastModified.Second);

            var driveItem = new CryptoDriveItem(fileName, folderPath, DriveItemType.File)
            {
                LastModified = lastModified,
                Size = fileInfo.Length
            };

            return driveItem;
        }

        // properties
        public static WatcherChangeTypes GetChangeType(this CryptoDriveItem newDriveItem, CryptoDriveItem oldDriveItem, bool compareSize)
        {
            WatcherChangeTypes changeType = default; // no change

            if (oldDriveItem == null)
                changeType = WatcherChangeTypes.Created;

            else if (newDriveItem.IsDeleted)
                changeType = WatcherChangeTypes.Deleted;

            else if (oldDriveItem.Id != newDriveItem.Id)
                throw new ArgumentException();

            else if (oldDriveItem.GetItemPath() != newDriveItem.GetItemPath())
                changeType = WatcherChangeTypes.Renamed;

            else if (newDriveItem.Type == DriveItemType.File)
            {
                if (compareSize)
                    if (oldDriveItem.LastModified != newDriveItem.LastModified
                     || oldDriveItem.Size != newDriveItem.Size)
                        changeType = WatcherChangeTypes.Changed;
                    else if (oldDriveItem.LastModified != newDriveItem.LastModified)
                        changeType = WatcherChangeTypes.Changed;
            }

            return changeType;
        }
    }
}
