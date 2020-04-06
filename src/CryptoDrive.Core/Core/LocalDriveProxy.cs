using CryptoDrive.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace CryptoDrive.Core
{
    public class LocalDriveProxy : IDriveProxy
    {
        #region Events

        public event EventHandler<string> FolderChanged;

        #endregion

        #region Fields

        private PollingWatcher _pollingWatcher;
        private IEnumerator<DriveItem> _fileEnumerator;

        #endregion

        #region Constructors

        public LocalDriveProxy(string basePath, string name, ILogger logger, TimeSpan pollInterval = default)
        {
            this.BasePath = basePath;
            this.Name = name;
            this.Logger = logger;

            logger.LogInformation($"Drive '{name}' is tracking folder '{basePath}'.");
            Directory.CreateDirectory(basePath);

            // polling watcher
            if (pollInterval == default)
                pollInterval = TimeSpan.FromSeconds(5);

            _pollingWatcher = new PollingWatcher(this.BasePath, pollInterval);
            _pollingWatcher.DriveChanged += this.OnDriveChanged;
            _pollingWatcher.EnableRaisingEvents = true;
        }

        #endregion

        #region Properties

        public string Name { get; }
        public string BasePath { get; }

        public bool EnableChangeTracking
        {
            get { return _pollingWatcher.EnableRaisingEvents; }
            set { _pollingWatcher.EnableRaisingEvents = value; }
        }

        private ILogger Logger { get; }

        #endregion

        #region Change Tracking

        public async Task ProcessDelta(Func<List<DriveItem>, Task> action,
                                       string folderPath,
                                       CryptoDriveContext context,
                                       CancellationToken cancellationToken)
        {
            var pageCounter = 0;

            while (true)
            {
                using (this.Logger.BeginScope(new Dictionary<string, object>
                {
                    [$"DeltaPage ({this.Name})"] = pageCounter
                }))
                {
                    (var deltaPage, var isLast) = await this.GetDeltaPageAsync(folderPath, context, cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await action?.Invoke(deltaPage);
                    pageCounter++;

                    // exit while loop
                    if (isLast)
                        break;
                }
            }
        }

        private Task<(List<DriveItem> DeltaPage, bool IsLast)> GetDeltaPageAsync(string folderPath,
                                                                                 CryptoDriveContext context,
                                                                                 CancellationToken cancellationToken)
        {
            var pageSize = 10;
            var deltaPage = new List<DriveItem>();

            if (_fileEnumerator is null)
                _fileEnumerator = this.SafelyEnumerateDriveItems(folderPath, context)
                    .Where(current => this.CheckPathAllowed(current.GetItemPath()))
                    .GetEnumerator();

            while (!cancellationToken.IsCancellationRequested && _fileEnumerator.MoveNext())
            {
                deltaPage.Add(_fileEnumerator.Current);

                if (deltaPage.Count == pageSize)
                    return Task.FromResult((deltaPage, false));
            }

            _fileEnumerator = null;

            return Task.FromResult((deltaPage, true));
        }

        #endregion

        #region CRUD

        public async Task<DriveItem> CreateOrUpdateAsync(DriveItem driveItem)
        {
            var fullPath = driveItem.GetAbsolutePath(this.BasePath);

            switch (driveItem.Type())
            {
                case DriveItemType.Folder:
                    Directory.CreateDirectory(fullPath);
                    break;

                case DriveItemType.File:

                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

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

                    break;

                case DriveItemType.RemoteItem:
                default:
                    throw new NotSupportedException();
            }

            return driveItem;
        }

        public Task<DriveItem> MoveAsync(DriveItem oldDriveItem, DriveItem newDriveItem)
        {
            var fullOldPath = oldDriveItem.GetAbsolutePath(this.BasePath);
            var fullNewPath = newDriveItem.GetAbsolutePath(this.BasePath);

            switch (newDriveItem.Type())
            {
                case DriveItemType.Folder:
                    Directory.Move(fullOldPath, fullNewPath);
                    break;

                case DriveItemType.File:
                    Directory.CreateDirectory(Path.GetDirectoryName(fullNewPath));
                    File.Move(fullOldPath, fullNewPath);
                    break;

                case DriveItemType.RemoteItem:
                default:
                    throw new NotSupportedException();
            }

            return Task.FromResult(newDriveItem);
        }

        public Task DeleteAsync(DriveItem driveItem)
        {
            string fullPath = driveItem.GetAbsolutePath(this.BasePath);

            switch (driveItem.Type())
            {
                case DriveItemType.Folder:
                    Directory.Delete(fullPath, recursive: true);
                    break;

                case DriveItemType.File:
                    File.Delete(fullPath);
                    break;

                case DriveItemType.RemoteItem:
                default:
                    throw new NotSupportedException();
            }

            return Task.CompletedTask;
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
                case DriveItemType.Folder:
                    result = Directory.Exists(fullPath);
                    break;

                case DriveItemType.File:
                    result = File.Exists(fullPath);
                    break;

                case DriveItemType.RemoteItem:
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
            var driveItemPath = driveItem.GetAbsolutePath(this.BasePath);

            switch (driveItem.Type())
            {
                case DriveItemType.Folder:
                    Directory.SetLastWriteTimeUtc(driveItemPath, driveItem.LastModified());
                    break;

                case DriveItemType.File:
                    File.SetLastWriteTimeUtc(driveItemPath, driveItem.LastModified());
                    break;

                case DriveItemType.RemoteItem:
                default:
                    throw new NotSupportedException();
            }

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
            if (driveItem.Type() == DriveItemType.File)
            {
                var fileInfo = new FileInfo(driveItem.GetAbsolutePath(this.BasePath));
                return Task.FromResult(fileInfo.ToDriveItem(this.BasePath));
            }
            else
                throw new ArgumentException();
        }

        #endregion

        #region Private

        private void OnDriveChanged(object source, DriveChangedEventArgs e)
        {
            foreach (var folderPath in e.FolderPaths)
            {
                this.FolderChanged?.Invoke(this, folderPath);
            }
        }

        private IEnumerable<DriveItem> SafelyEnumerateDriveItems(string folderPath, CryptoDriveContext context, [CallerMemberName] string callerName = "")
        {
            var forceNew = callerName == nameof(this.SafelyEnumerateDriveItems);
            var driveItems = Enumerable.Empty<DriveItem>();
            var absoluteFolderPath = folderPath.ToAbsolutePath(this.BasePath);

            try
            {
                // get all folders in current folder
                driveItems = driveItems.Concat(Directory.EnumerateDirectories(absoluteFolderPath, "*", SearchOption.TopDirectoryOnly)
                    .SelectMany(current =>
                    {
                        RemoteState oldRemoteState = null;
                        var driveItems = new DirectoryInfo(current).ToDriveItem(this.BasePath);
                        var folderPath = current.Substring(this.BasePath.Length).NormalizeSlashes();

                        if (!forceNew)
                            oldRemoteState = context.RemoteStates.FirstOrDefault(remoteState => remoteState.GetItemPath() == folderPath
                                                                                             && remoteState.Type == DriveItemType.Folder);

                        // When we know that the parent folder is not in the database (oldRemoteState == null),
                        // the children aren't there neither (forceNew = true).
                        if (forceNew || oldRemoteState == null)
                            return this.SafelyEnumerateDriveItems(folderPath, context)
                                       .Concat(new DriveItem[] { driveItems });
                        else
                            return new List<DriveItem> { driveItems };
                    }));

                // get all files in current folder
                driveItems = driveItems.Concat(Directory.EnumerateFiles(absoluteFolderPath)
                    .SelectMany(current =>
                    {
                        var driveInfo = new FileInfo(current).ToDriveItem(this.BasePath);
                        return new List<DriveItem> { driveInfo };
                    }).ToList());

                // get all deleted items
                var remoteStates = context.RemoteStates.Where(current => current.Path == folderPath);              

                var deletedItems = remoteStates.Where(current =>
                {
                    switch (current.Type)
                    {
                        case DriveItemType.Folder:
                            return !Directory.Exists(current.GetItemPath().ToAbsolutePath(this.BasePath));

                        case DriveItemType.File:
                            return !File.Exists(current.GetItemPath().ToAbsolutePath(this.BasePath));

                        case DriveItemType.RemoteItem:
                        default:
                            throw new NotSupportedException();
                    }
                }).Select(current => current.ToDriveItem(deleted: true));

                driveItems = driveItems.Concat(deletedItems);

                return driveItems;
            }
            catch (UnauthorizedAccessException)
            {
                return driveItems;
            }
        }

        private bool CheckPathAllowed(string filePath)
        {
            return true;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _pollingWatcher.Dispose();
        }

        #endregion
    }
}
