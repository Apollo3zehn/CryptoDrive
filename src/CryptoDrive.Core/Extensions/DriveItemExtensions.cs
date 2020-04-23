using CryptoDrive.Core;
using CryptoDrive.Drives;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.IO;

namespace CryptoDrive.Extensions
{
    public static class DriveItemExtensions
    {
        // from remote state to drive item and vice versa
        public static RemoteState ToRemoteState(this DriveItem driveItem)
        {
            var type = driveItem.Type();

            return new RemoteState()
            {
                Path = driveItem.ParentReference.Path,
                Id = driveItem.Id,
                Name = driveItem.Name,
                LastModified = driveItem.FileSystemInfo.LastModifiedDateTime.Value.UtcDateTime,
                Size = type == DriveItemType.File ? driveItem.Size.Value : 0,
                Type = type
            };
        }

        public static DriveItem ToDriveItem(this RemoteState remoteState, bool deleted = false)
        {
            return new DriveItem()
            {
                Deleted = deleted ? new Deleted() : null,
                File = remoteState.Type == DriveItemType.File ? new Microsoft.Graph.File() : null,
                FileSystemInfo = new Microsoft.Graph.FileSystemInfo()
                {
                    LastModifiedDateTime = remoteState.LastModified
                },
                Folder = remoteState.Type == DriveItemType.Folder ? new Folder() : null,
                Id = remoteState.Id,
                Name = remoteState.Name,
                ParentReference = new ItemReference() { Path = remoteState.Path },
                RemoteItem = remoteState.Type == DriveItemType.RemoteItem ? new RemoteItem() : null,
                Size = remoteState.Size
            };
        }

        // from drive item to drive item
        public static DriveItem ToCreateFolderDriveItem(this DriveItem driveItem)
        {
            return new DriveItem
            {
                Name = driveItem.Name,
                Folder = new Folder(),
                AdditionalData = new Dictionary<string, object>()
                {
                    {"@microsoft.graph.conflictBehavior", "replace"}
                }
            };
        }

        // from x to drive item
        public static DriveItem ToDriveItem(this string itemPath, DriveItemType driveItemType)
        {
            if (itemPath == "/")
                return itemPath.ToSpecialDriveItem();

            var itemName = Path.GetFileName(itemPath);
            var folderPath = Path.GetDirectoryName(itemPath).NormalizeSlashes();

            if (driveItemType == DriveItemType.RemoteItem)
                throw new NotSupportedException();

            var driveItem = new DriveItem()
            {
                File = driveItemType == DriveItemType.File ? new Microsoft.Graph.File() : null,
                Folder = driveItemType == DriveItemType.Folder ? new Folder() : null,
                Name = itemName,
                ParentReference = new ItemReference() { Path = folderPath }
            };

            return driveItem;
        }

        private static DriveItem ToSpecialDriveItem(this string relativePath)
        {
            var itemName = "";
            var folderPath = relativePath;

            var driveItem = new DriveItem()
            {
                Folder = new Folder(),
                Name = itemName,
                ParentReference = new ItemReference() { Path = folderPath }
            };

            return driveItem;
        }

        public static DriveItem ToDriveItem(this DirectoryInfo folderInfo, string basePath)
        {
            var folderName = folderInfo.Name;
            var folderPath = folderInfo.Parent.FullName.Substring(basePath.Length).NormalizeSlashes();

            var driveItem = new DriveItem()
            {
                FileSystemInfo = new Microsoft.Graph.FileSystemInfo()
                {
                    LastModifiedDateTime = folderInfo.LastWriteTimeUtc
                },
                Folder = new Folder(),
                Name = folderName,
                ParentReference = new ItemReference() { Path = folderPath }
            };

            return driveItem;
        }

        public static DriveItem ToDriveItem(this FileInfo fileInfo, string basePath)
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

            var driveItem = new DriveItem()
            {
                AdditionalData = new Dictionary<string, object>()
                {
                    [OneDriveConstants.DownloadUrl] = new Uri(fileInfo.FullName),
                },
                File = new Microsoft.Graph.File(),
                FileSystemInfo = new Microsoft.Graph.FileSystemInfo()
                {
                    LastModifiedDateTime = lastModified
                },
                Name = fileName,
                ParentReference = new ItemReference() { Path = folderPath },
                Size = fileInfo.Length
            };

            return driveItem;
        }

        // from driveItem to x
        public static DriveItemUploadableProperties ToUploadableProperties(this DriveItem driveItem)
        {
            return new DriveItemUploadableProperties()
            {
                FileSystemInfo = driveItem.FileSystemInfo
            };
        }

        // path handling
        public static Uri Uri(this DriveItem driveItem)
        {
            if (driveItem.Type() == DriveItemType.File)
                return new Uri((string)driveItem.AdditionalData[OneDriveConstants.DownloadUrl]);

            return null;
        }

        public static string GetItemPath(this DriveItem driveItem)
        {
            return Utilities.PathCombine(driveItem.ParentReference.Path, driveItem.Name);
        }

        public static string GetAbsolutePath(this DriveItem driveItem, string basePath)
        {
            return driveItem.GetItemPath().ToAbsolutePath(basePath);
        }

        public static DriveItem MemberwiseClone(this DriveItem driveItem)
        {
            Dictionary<string, object> additionalData = null;
            ItemReference parentReference = null;
            Microsoft.Graph.FileSystemInfo fileSystemInfo = null;

            if (driveItem.AdditionalData != null)
                additionalData = new Dictionary<string, object>() { [OneDriveConstants.DownloadUrl] = driveItem.AdditionalData[OneDriveConstants.DownloadUrl] };

            if (driveItem.ParentReference != null)
                parentReference = new ItemReference() { Path = driveItem.ParentReference.Path };

            if (driveItem.FileSystemInfo != null)
            {
                fileSystemInfo = new Microsoft.Graph.FileSystemInfo()
                {
                    CreatedDateTime = driveItem.FileSystemInfo.CreatedDateTime,
                    LastAccessedDateTime = driveItem.FileSystemInfo.LastAccessedDateTime,
                    LastModifiedDateTime = driveItem.FileSystemInfo.LastModifiedDateTime
                };
            }

            return new DriveItem()
            {
                AdditionalData = additionalData,
                Content = driveItem.Content,
                Deleted = driveItem.Deleted == null ? null : new Deleted(),
                File = driveItem.File == null ? null : new Microsoft.Graph.File(),
                Folder = driveItem.Folder == null ? null : new Folder(),
                Id = driveItem.Id,
                Name = driveItem.Name,
                ParentReference = parentReference,
                RemoteItem = driveItem.RemoteItem == null ? null : new RemoteItem(),
                Size = driveItem.Size,
                FileSystemInfo = fileSystemInfo,
            };
        }

        // properties
        public static WatcherChangeTypes GetChangeType(this DriveItem newDriveItem, DriveItem oldDriveItem, bool compareSize)
        {
            WatcherChangeTypes changeType = default; // no change

            if (oldDriveItem == null)
                changeType = WatcherChangeTypes.Created;

            else if (newDriveItem.Deleted != null)
                changeType = WatcherChangeTypes.Deleted;

            else if (oldDriveItem.Id != newDriveItem.Id)
                throw new ArgumentException();

            else if (oldDriveItem.GetItemPath() != newDriveItem.GetItemPath())
                changeType = WatcherChangeTypes.Renamed;

            else if (newDriveItem.Type() == DriveItemType.File)
            {
                if (compareSize)
                    if (oldDriveItem.LastModified() != newDriveItem.LastModified()
                     || oldDriveItem.Size != newDriveItem.Size)
                        changeType = WatcherChangeTypes.Changed;
                else
                    if (oldDriveItem.LastModified() != newDriveItem.LastModified())
                        changeType = WatcherChangeTypes.Changed;
            }

            return changeType;
        }

        public static DriveItemType Type(this DriveItem driveItem)
        {
            if (driveItem.File != null)
                return DriveItemType.File;

            else if (driveItem.Folder != null)
                return DriveItemType.Folder;

            else if (driveItem.RemoteItem != null)
                return DriveItemType.RemoteItem;

            else
                throw new ArgumentException();
        }

        public static DateTime LastModified(this DriveItem driveItem)
        {
            return driveItem.FileSystemInfo.LastModifiedDateTime.Value.UtcDateTime;
        }
    }
}
