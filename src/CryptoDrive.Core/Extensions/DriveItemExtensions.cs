using CryptoDrive.Core;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.IO;
using File = Microsoft.Graph.File;
using FileSystemInfo = Microsoft.Graph.FileSystemInfo;

namespace CryptoDrive.Extensions
{
    public static class DriveItemExtensions
    {
        // from remote state to drive item and vice versa
        public static RemoteState ToRemoteState(this DriveItem driveItem)
        {
            GraphItemType type;

            if (driveItem.Folder != null)
                type = GraphItemType.Folder;
            else if (driveItem.File != null)
                type = GraphItemType.File;
            else if (driveItem.RemoteItem != null)
                type = GraphItemType.RemoteItem;
            else
                throw new ArgumentException();

            return new RemoteState()
            {
                ETag = driveItem.ETag,
                Id = driveItem.Id,
                IsDeleted = driveItem.Deleted != null,
                LastModified = driveItem.FileSystemInfo.LastModifiedDateTime.Value.DateTime,
                Path = driveItem.GetPath(),
                Size = driveItem.Size.Value,
                Type = type
            };
        }

        public static DriveItem ToDriveItem(this RemoteState remoteState)
        {
            return new DriveItem()
            {
                Deleted = remoteState.IsDeleted ? new Deleted() : null,
                ETag = remoteState.ETag,
                FileSystemInfo = new FileSystemInfo()
                {
                    LastModifiedDateTime = remoteState.LastModified
                },
                Id = remoteState.Id,
                Name = Path.GetFileName(remoteState.Path),
                ParentReference = new ItemReference()
                {
                    Path = $"{CryptoDriveConstants.PathPrefix}{Path.GetDirectoryName(remoteState.Path)}",
                },
                Size = remoteState.Size
            };
        }

        // from x to drive item
        public static DriveItem ToDriveItem(this string filePath)
        {
            return new DriveItem()
            {
                AdditionalData = new Dictionary<string, object>(),
                File = new File(),
                Name = Path.GetFileName(filePath),
                ParentReference = new ItemReference()
                {
                    Path = $"{CryptoDriveConstants.PathPrefix}{Path.GetDirectoryName(filePath)}"
                }
            };
        }

        public static DriveItem ToDriveItem(this DirectoryInfo folderInfo, string basePath)
        {
            return new DriveItem()
            {
                FileSystemInfo = new FileSystemInfo()
                {
                    LastModifiedDateTime = folderInfo.LastWriteTimeUtc
                },
                Folder = new Folder(),
                Id = folderInfo.Name,
                Name = folderInfo.Name,
                ParentReference = new ItemReference()
                {
                    Path = folderInfo.Parent.FullName.Replace(basePath, CryptoDriveConstants.PathPrefix)
                }
            };
        }

        public static DriveItem ToDriveItem(this FileInfo fileInfo, string basePath)
        {
            return new DriveItem()
            {
                AdditionalData = new Dictionary<string, object>()
                {
                    [CryptoDriveConstants.DownloadUrl] = new Uri(fileInfo.FullName),
                },
                File = new File(),
                FileSystemInfo = new FileSystemInfo()
                {
                    LastModifiedDateTime = fileInfo.LastWriteTimeUtc
                },
                Id = fileInfo.Name,
                Name = fileInfo.Name,
                ParentReference = new ItemReference()
                {
                    Path = fileInfo.DirectoryName.Replace(basePath, CryptoDriveConstants.PathPrefix)
                },
                Size = fileInfo.Length
            };
        }

        // path handling
        public static Uri Uri(this DriveItem driveItem)
        {
            if (driveItem.Type() == GraphItemType.File)
                return driveItem.AdditionalData[CryptoDriveConstants.DownloadUrl] as Uri;

            return null;
        }

        public static void SetUri(this DriveItem driveItem, Uri newUri)
        {
            if (driveItem.Type() == GraphItemType.File)
                driveItem.AdditionalData[CryptoDriveConstants.DownloadUrl] = newUri;
        }

        public static string GetPath(this DriveItem driveItem)
        {
            return $"{driveItem.ParentReference.Path.Substring(CryptoDriveConstants.PathPrefix.Length) + driveItem.Name}";
        }

        public static string GetAbsolutePath(this DriveItem driveItem, string basePath)
        {
            return $"{driveItem.GetPath()}".ToAbsolutePath(basePath);
        }

        public static string GetConflictFilePath(this DriveItem driveItem)
        {
            return driveItem.GetPath().ToConflictFilePath(driveItem.LastModified());
        }

        public static DriveItem ToConflict(this DriveItem driveItem)
        {
            driveItem.Name = driveItem.GetConflictFilePath();

            return driveItem;
        }

        public static DriveItem MemberwiseClone(this DriveItem driveItem)
        {
            Dictionary<string, object> additionalData = null;
            ItemReference parentReference = null;
            FileSystemInfo fileSystemInfo = null;

            if (driveItem.AdditionalData != null)
                additionalData = new Dictionary<string, object>() { [CryptoDriveConstants.DownloadUrl] = driveItem.AdditionalData[CryptoDriveConstants.DownloadUrl] };

            if (driveItem.ParentReference != null)
                parentReference = new ItemReference() { Path = driveItem.ParentReference.Path };

            if (driveItem.FileSystemInfo != null)
            {
                fileSystemInfo = new FileSystemInfo()
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
                File = driveItem.File == null ? null : new File(),
                Folder = driveItem.Folder == null ? null : new Folder(),
                Id = driveItem.Id,
                Name = driveItem.Name,
                ETag = driveItem.ETag,
                ParentReference = parentReference,
                RemoteItem = driveItem.RemoteItem == null ? null : new RemoteItem(),
                Size = driveItem.Size,
                FileSystemInfo = fileSystemInfo,
            };
        }

        // properties
        public static WatcherChangeTypes GetChangeType(this DriveItem newDriveItem, DriveItem oldDriveItem = null)
        {
            if (oldDriveItem == null)
                return WatcherChangeTypes.Created;

            else if (oldDriveItem.Id != newDriveItem.Id)
                throw new ArgumentException();

            else if (oldDriveItem.GetPath() != newDriveItem.GetPath())
                return WatcherChangeTypes.Renamed;

            else if (oldDriveItem.CTag != oldDriveItem.CTag)
                return WatcherChangeTypes.Changed;

            else if (newDriveItem.Deleted != null)
                return WatcherChangeTypes.Deleted;

            // no change
            else
                return 0;
        }

        public static bool IsDeleted(this DriveItem driveItem)
        {
            return driveItem.Deleted != null;
        }

        public static GraphItemType Type(this DriveItem driveItem)
        {
            if (driveItem.File != null)
                return GraphItemType.File;
            else if (driveItem.Folder != null)
                return GraphItemType.Folder;
            else if (driveItem.RemoteItem != null)
                return GraphItemType.RemoteItem;
            else
                throw new ArgumentException();
        }

        public static DateTime LastModified(this DriveItem driveItem)
        {
            return driveItem.FileSystemInfo.LastModifiedDateTime.Value.DateTime;
        }
    }
}
