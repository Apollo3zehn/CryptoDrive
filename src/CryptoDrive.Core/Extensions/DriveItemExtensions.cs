using CryptoDrive.Core;
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
                Path = driveItem.ParentReference.Path.Substring(CryptoDriveConstants.PathPrefix.Length),
                Id = driveItem.Id,
                Name = driveItem.Name,
                LastModified = driveItem.FileSystemInfo.LastModifiedDateTime.Value.UtcDateTime,
                QuickXorHash = type == DriveItemType.File && driveItem.File.Hashes != null ? driveItem.QuickXorHash() : null,
                Size = type == DriveItemType.File ? driveItem.Size.Value : 0,
                Type = type
            };
        }

        public static DriveItem ToDriveItem(this RemoteState remoteState, bool deleted = false)
        {
            return new DriveItem()
            {
                Deleted = deleted ? new Deleted() : null,
                File = remoteState.Type == DriveItemType.File ? new Microsoft.Graph.File() { Hashes = new Hashes() { QuickXorHash = remoteState.QuickXorHash } } : null,
                FileSystemInfo = new Microsoft.Graph.FileSystemInfo()
                {
                    LastModifiedDateTime = remoteState.LastModified
                },
                Folder = remoteState.Type == DriveItemType.Folder ? new Folder() : null,
                Id = remoteState.Id,
                Name = remoteState.Name,
                ParentReference = new ItemReference()
                {
                    Path = $"{CryptoDriveConstants.PathPrefix}{remoteState.Path}",
                },
                RemoteItem = remoteState.Type == DriveItemType.RemoteItem ? new RemoteItem() : null,
                Size = remoteState.Size
            };
        }

        // from x to drive item
        public static DriveItem ToDriveItem(this FileSystemEventArgs fileSystemEventArgs, string basePath)
        {
            var fileInfo = new FileInfo(fileSystemEventArgs.FullPath);

            if (fileSystemEventArgs.ChangeType == WatcherChangeTypes.Deleted)
            {
                var relativePath = fileSystemEventArgs.FullPath.Substring(basePath.Length);
                var fileName = Path.GetFileName(relativePath);
                var folderPath = Path.GetDirectoryName(relativePath).NormalizeSlashes();

                var driveItem = new DriveItem()
                {
                    Deleted = new Deleted(),
                    File = new Microsoft.Graph.File(),
                    FileSystemInfo = new Microsoft.Graph.FileSystemInfo()
                    {
                        LastModifiedDateTime = DateTime.UtcNow
                    },
                    Name = fileName,
                    ParentReference = new ItemReference()
                    {
                        Path = $"{CryptoDriveConstants.PathPrefix}{folderPath}",
                    },
                    Size = 0
                };

                driveItem.Id = driveItem.GetItemPath();

                return driveItem;
            }
            else
            {
                return fileInfo.ToDriveItem(basePath);
            }
        }

        public static DriveItem ToDriveItem(this string relativePath, DriveItemType driveItemType)
        {
            var itemName = Path.GetFileName(relativePath);
            var folderPath = Path.GetDirectoryName(relativePath).NormalizeSlashes();

            if (driveItemType == DriveItemType.RemoteItem)
                throw new NotSupportedException();

            var driveItem = new DriveItem()
            {
                File = driveItemType == DriveItemType.File ? new Microsoft.Graph.File() : null,
                Folder = driveItemType == DriveItemType.Folder ? new Folder() : null,
                Name = itemName,
                ParentReference = new ItemReference()
                {
                    Path = $"{CryptoDriveConstants.PathPrefix}{folderPath}"
                }
            };

            driveItem.Id = driveItem.GetItemPath();

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
                Id = folderInfo.Name,
                Name = folderName,
                ParentReference = new ItemReference()
                {
                    Path = $"{CryptoDriveConstants.PathPrefix}{folderPath}"
                }
            };

            driveItem.Id = driveItem.GetItemPath();

            return driveItem;
        }

        public static DriveItem ToDriveItem(this FileInfo fileInfo, string basePath)
        {
            var fileName = fileInfo.Name;
            var folderPath = fileInfo.DirectoryName.Substring(basePath.Length).NormalizeSlashes();

            var driveItem = new DriveItem()
            {
                AdditionalData = new Dictionary<string, object>()
                {
                    [CryptoDriveConstants.DownloadUrl] = new Uri(fileInfo.FullName),
                },
                File = new Microsoft.Graph.File(),
                FileSystemInfo = new Microsoft.Graph.FileSystemInfo()
                {
                    LastModifiedDateTime = fileInfo.LastWriteTimeUtc
                },
                Name = fileName,
                ParentReference = new ItemReference()
                {
                    Path = $"{CryptoDriveConstants.PathPrefix}{folderPath}"
                },
                Size = fileInfo.Length
            };

            driveItem.Id = driveItem.GetItemPath();

            return driveItem;
        }

        // path handling
        public static Uri Uri(this DriveItem driveItem)
        {
            if (driveItem.Type() == DriveItemType.File)
                return driveItem.AdditionalData[CryptoDriveConstants.DownloadUrl] as Uri;

            return null;
        }

        public static void SetUri(this DriveItem driveItem, Uri newUri)
        {
            if (driveItem.Type() == DriveItemType.File)
                driveItem.AdditionalData[CryptoDriveConstants.DownloadUrl] = newUri;
        }

        public static string GetItemPath(this DriveItem driveItem)
        {
            var folderPath = $"{driveItem.ParentReference.Path.Substring(CryptoDriveConstants.PathPrefix.Length)}";

            return PathHelper.Combine(folderPath, driveItem.Name);
        }

        public static string GetAbsolutePath(this DriveItem driveItem, string basePath)
        {
            return driveItem.GetItemPath().ToAbsolutePath(basePath);
        }

        public static DriveItem ToConflict(this DriveItem driveItem)
        {
            driveItem.Name = driveItem.Name.ToConflictFileName(driveItem.LastModified());

            return driveItem;
        }

        public static DriveItem MemberwiseClone(this DriveItem driveItem)
        {
            Dictionary<string, object> additionalData = null;
            ItemReference parentReference = null;
            Microsoft.Graph.FileSystemInfo fileSystemInfo = null;
            Hashes hashes = null;

            if (driveItem.AdditionalData != null)
                additionalData = new Dictionary<string, object>() { [CryptoDriveConstants.DownloadUrl] = driveItem.AdditionalData[CryptoDriveConstants.DownloadUrl] };

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

            if (driveItem.File?.Hashes != null)
            {
                hashes = new Hashes() { QuickXorHash = driveItem.QuickXorHash() };
            }

            return new DriveItem()
            {
                AdditionalData = additionalData,
                Content = driveItem.Content,
                Deleted = driveItem.Deleted == null ? null : new Deleted(),
                File = driveItem.File == null ? null : new Microsoft.Graph.File() { Hashes = hashes },
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
        public static WatcherChangeTypes GetChangeType(this DriveItem newDriveItem, DriveItem oldDriveItem = null)
        {
            if (oldDriveItem == null)
                return WatcherChangeTypes.Created;

            if (newDriveItem.Deleted != null)
                return WatcherChangeTypes.Deleted;

            else if (oldDriveItem.Id != newDriveItem.Id)
                throw new ArgumentException();

            else if (oldDriveItem.GetItemPath() != newDriveItem.GetItemPath())
                return WatcherChangeTypes.Renamed;

            else if (newDriveItem.Type() == DriveItemType.File &&
                    (oldDriveItem.LastModified() != newDriveItem.LastModified() 
                            || oldDriveItem.Size != newDriveItem.Size))
                return WatcherChangeTypes.Changed;

            // no change
            else
                return 0;
        }

        public static bool IsDeleted(this DriveItem driveItem)
        {
            return driveItem.Deleted != null;
        }

        public static string QuickXorHash(this DriveItem driveItem)
        {
            return driveItem.File.Hashes.QuickXorHash;
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
