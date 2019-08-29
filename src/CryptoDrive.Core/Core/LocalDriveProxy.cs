using CryptoDrive.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace CryptoDrive.Core
{
    public class LocalDriveProxy : IDriveProxy
    {
        #region Fields

        private bool _isFirstDelta;
        private FileSystemWatcher _fileWatcher;
        private IEnumerator<string> _fileEnumerator;
        private ConcurrentQueue<FileSystemEventArgs> _localChanges;

        #endregion

        #region Constructors

        public LocalDriveProxy(string basePath, string name, ILogger logger)
        {
            this.BasePath = basePath;
            this.Name = name;
            this.Logger = logger;

            logger.LogInformation($"Drive '{name}' is tracking folder '{basePath}'.");
            Directory.CreateDirectory(basePath);

            _isFirstDelta = true;

            // file system watcher
            _localChanges = new ConcurrentQueue<FileSystemEventArgs>();

            _fileWatcher = new FileSystemWatcher()
            {
                IncludeSubdirectories = true,
                InternalBufferSize = 64000,
                NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite,
                Path = this.BasePath,
            };

            _fileWatcher.Changed += this.OnDriveItemChanged;
            _fileWatcher.Created += this.OnDriveItemChanged;
            _fileWatcher.Deleted += this.OnDriveItemChanged;
            _fileWatcher.Renamed += this.OnDriveItemChanged;

            _fileWatcher.EnableRaisingEvents = true;
        }

        #endregion

        #region Properties

        public string Name { get; }
        public string BasePath { get; }
        private ILogger Logger { get; }

        #endregion

        #region Change Tracking

        public async Task ProcessDelta(Func<List<DriveItem>, Task> action)
        {
            var pageCounter = 0;

            while (true)
            {
                using (this.Logger.BeginScope(new Dictionary<string, object>
                {
                    [$"DeltaPage ({this.Name})"] = pageCounter
                }))
                {
                    (var deltaPage, var isLast) = await this.GetDeltaPageAsync();

                    await action?.Invoke(deltaPage);
                    pageCounter++;

                    // exit while loop
                    if (isLast)
                        break;
                }
            }
        }

        public Task<(List<DriveItem> DeltaPage, bool IsFirstDelta)> GetDeltaPageAsync()
        {
            var pageSize = 10;
            var deltaPage = new List<DriveItem>();

            // simply enumerate all items in drive
            if (_isFirstDelta)
            {
                if (_fileEnumerator is null)
                    _fileEnumerator = DirectoryHelper.SafelyEnumerateFiles(this.BasePath, "*", SearchOption.AllDirectories)
                                                     .Where(current => this.CheckPathAllowed(current))
                                                     .GetEnumerator();

                while (_fileEnumerator.MoveNext())
                {
                    deltaPage.Add(new FileInfo(_fileEnumerator.Current).ToDriveItem(this.BasePath));

                    if (deltaPage.Count == pageSize)
                        return Task.FromResult((deltaPage, false));
                }

                _isFirstDelta = false;
            }

            // use file system watcher
            else
            {
                for (int i = 0; i < pageSize; i++)
                {
                    if (_localChanges.TryDequeue(out var fileSystemEventArgs))
                    {
                        DriveItem driveItem = null;

                        // determine if directory or file

                        deltaPage.Add(driveItem);

                        if (deltaPage.Count == pageSize)
                            return Task.FromResult((deltaPage, false));
                    }
                }
            }

            return Task.FromResult((deltaPage, true));
        }

        #endregion

        #region CRUD

        public async Task<DriveItem> CreateOrUpdateAsync(DriveItem driveItem)
        {
            var fullPath = driveItem.GetAbsolutePath(this.BasePath);

            if (driveItem.Content != null)
            {
                using (var stream = File.OpenWrite(fullPath))
                {
                    driveItem.Content.Seek(0, SeekOrigin.Begin);
                    driveItem.Content.CopyTo(stream);
                }
            }
            else
            {
                await new WebClient().DownloadFileTaskAsync(driveItem.Uri(), fullPath);
            }

            File.SetLastWriteTimeUtc(fullPath, driveItem.FileSystemInfo.LastModifiedDateTime.Value.DateTime);

            return driveItem;
        }

        public Task<DriveItem> MoveAsync(DriveItem oldDriveItem, DriveItem newDriveItem)
        {
            // ...

            return Task.FromResult(newDriveItem);
        }

        public Task<DriveItem> DeleteAsync(DriveItem driveItem)
        {
            string fullPath = driveItem.GetAbsolutePath(this.BasePath);

            switch (driveItem.Type())
            {
                case GraphItemType.Folder:
                    Directory.Delete(fullPath);
                    break;

                case GraphItemType.File:
                    File.Delete(fullPath);
                    break;

                case GraphItemType.RemoteItem:
                    throw new NotSupportedException();
                default:
                    throw new NotSupportedException();
            }

            return Task.FromResult(driveItem);
        }

        #endregion

        #region File Info

        public Task<Uri> GetDownloadUriAsync(DriveItem driveItem)
        {
            return Task.FromResult(new Uri(driveItem.GetAbsolutePath(this.BasePath)));
        }

        public Task<bool> ExistsAsync(DriveItem driveItem)
        {
            bool result;
            string fullPath = driveItem.GetAbsolutePath(this.BasePath);

            switch (driveItem.Type())
            {
                case GraphItemType.Folder:
                    result = Directory.Exists(fullPath);
                    break;

                case GraphItemType.File:
                    result = File.Exists(fullPath);
                    break;

                case GraphItemType.RemoteItem:
                    throw new NotSupportedException();
                default:
                    throw new NotSupportedException();
            }

            return Task.FromResult(result);
        }

        public Task<DateTime> GetLastWriteTimeUtcAsync(DriveItem driveItem)
        {
            return Task.FromResult(File.GetLastWriteTimeUtc(driveItem.GetAbsolutePath(this.BasePath)));
        }

        public Task SetLastWriteTimeUtcAsync(DriveItem driveItem)
        {
            File.SetLastWriteTimeUtc(driveItem.GetAbsolutePath(this.BasePath), driveItem.LastModified());

            return Task.CompletedTask;
        }

        public Task<string> GetHashAsync(DriveItem driveItem)
        {
            var _hashAlgorithm = new QuickXorHash();

            using (var stream = File.OpenRead(driveItem.GetAbsolutePath(this.BasePath)))
            {
                return Task.FromResult(Convert.ToBase64String(_hashAlgorithm.ComputeHash(stream)));
            }
        }

        public Task<DriveItem> ToFullDriveItem(DriveItem driveItem)
        {
            if (driveItem.Type() == GraphItemType.File)
            {
                var fileInfo = new FileInfo(driveItem.GetAbsolutePath(this.BasePath));
                return Task.FromResult(fileInfo.ToDriveItem(this.BasePath));
            }
            else
                throw new ArgumentException();
        }

        #endregion

        #region Private

        private void OnDriveItemChanged(object source, FileSystemEventArgs e)
        {
            _localChanges.Enqueue(e);
        }

        private bool CheckPathAllowed(string filePath)
        {
            return true;
        }

        #endregion
    }
}
