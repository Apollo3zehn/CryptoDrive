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
            var type = driveItem.Type();

            return new RemoteState()
            {
                QuickXorHash = type == GraphItemType.File && driveItem.File.Hashes != null ? driveItem.QuickXorHash() : null,
                Id = driveItem.Id,
                LastModified = driveItem.FileSystemInfo.LastModifiedDateTime.Value.UtcDateTime,
                Path = driveItem.GetPath(),
                Size = type == GraphItemType.File ? driveItem.Size.Value : 0,
                Type = type
            };
        }

        public static DriveItem ToDriveItem(this RemoteState remoteState)
        {
            var fileName = Path.GetFileName(remoteState.Path);
            var folderPath = Path.GetDirectoryName(remoteState.Path).NormalizeSlashes();

            return new DriveItem()
            {
                File = remoteState.Type == GraphItemType.File ? new File() { Hashes = new Hashes() { QuickXorHash = remoteState.QuickXorHash } } : null,
                FileSystemInfo = new FileSystemInfo()
                {
                    LastModifiedDateTime = remoteState.LastModified
                },
                Folder = remoteState.Type == GraphItemType.Folder ? new Folder() : null,
                Id = remoteState.Id,
                Name = fileName,
                ParentReference = new ItemReference()
                {
                    Path = $"{CryptoDriveConstants.PathPrefix}{folderPath}",
                },
                RemoteItem = remoteState.Type == GraphItemType.RemoteItem ? new RemoteItem() : null,
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
                    Path = folderInfo.Parent.FullName.Replace(basePath, CryptoDriveConstants.PathPrefix).NormalizeSlashes()
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
                    Path = fileInfo.DirectoryName.Replace(basePath, CryptoDriveConstants.PathPrefix).NormalizeSlashes()
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
            return $"{driveItem.ParentReference.Path.Substring(CryptoDriveConstants.PathPrefix.Length)}/{driveItem.Name}";
        }

        public static string GetAbsolutePath(this DriveItem driveItem, string basePath)
        {
            return driveItem.GetPath().ToAbsolutePath(basePath);
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
            Hashes hashes = null;

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

            if (driveItem.File?.Hashes != null)
            {
                hashes = new Hashes() { QuickXorHash = driveItem.QuickXorHash() };
            }

            return new DriveItem()
            {
                AdditionalData = additionalData,
                Content = driveItem.Content,
                Deleted = driveItem.Deleted == null ? null : new Deleted(),
                File = driveItem.File == null ? null : new File() { Hashes = hashes },
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

            else if (oldDriveItem.Id != newDriveItem.Id)
                throw new ArgumentException();

            else if (oldDriveItem.GetPath() != newDriveItem.GetPath())
                return WatcherChangeTypes.Renamed;

            else if (oldDriveItem.LastModified() != newDriveItem.LastModified())
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


        public static string QuickXorHash(this DriveItem driveItem)
        {
            return driveItem.File.Hashes.QuickXorHash;
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
            return driveItem.FileSystemInfo.LastModifiedDateTime.Value.UtcDateTime;
        }
    }
}
