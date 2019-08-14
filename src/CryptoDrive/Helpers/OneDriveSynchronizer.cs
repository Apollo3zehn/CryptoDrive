using CryptoDrive.Extensions;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace CryptoDrive.Helpers
{
    public class OneDriveSynchronizer
    {
        private string _token;
        private string _rootFolderPath;

        private WebClient _webClient;
        private OneDriveContext _dbContext;
        private GraphServiceClient _graphClient;

        private object _dbContextLock;

        // high level
        public async Task SynchronizeTheUniverse(GraphServiceClient graphClient, OneDriveContext dbContext)
        {
            _graphClient = graphClient;
            _dbContext = dbContext;

            _rootFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CryptoDrive");
            _webClient = new WebClient();
            _dbContextLock = new object();

            if (!File.Exists("CryptoDrive.db"))
            {
                _token = null;
                await this.BuildIndex();
            }
        }

        private async Task BuildIndex()
        {
            // prepare database
            _dbContext.Database.EnsureCreated();
            //_dbContext.Database.ExecuteSqlRaw($"DELETE FROM {nameof(RemoteState)};");
            //_dbContext.Database.ExecuteSqlRaw($"DELETE FROM {nameof(LocalState)};");

            await this.ProcessRemoteDelta(async deltaPage =>
            {
                // select remote states and add them to the DB context
                var tasks = deltaPage.Select(async driveItem =>
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

                    var remoteState = new RemoteState()
                    {
                        Id = driveItem.Id,
                        Path = Path.Combine(driveItem.ParentReference.Path.Substring("/drive/root:".Length), driveItem.Name),
                        ETag = driveItem.ETag,
                        Size = driveItem.Size.Value,
                        Type = type,
                        LastModified = driveItem.FileSystemInfo.LastModifiedDateTime.Value,
                    };

                    // synchronize (download)
                    var localPath = Path.Combine(_rootFolderPath, remoteState.Path);

                    if (remoteState.Type == GraphItemType.File)
                    {
                        var downloadUri = new Uri(driveItem.AdditionalData["@microsoft.graph.downloadUrl"].ToString());
                        await this.SyncRemoteFile(remoteState, localPath, downloadUri);
                    }
                    else if (remoteState.Type == GraphItemType.Folder)
                    {
                        this.SyncRemoteFolder(localPath);
                    }

                    return remoteState;
                });

                await Task.WhenAll(tasks);
                var remoteStates = tasks.Select(task => task.Result).Where(state => state.Path != "root");

                _dbContext.RemoteStates.AddRange(remoteStates);
                await _dbContext.SaveChangesAsync();
            });

            await this.ProcessLocalDelta(async deltaPage =>
            {
                var tasks = deltaPage.Select(async localFilePath =>
                {
                    var remoteFilePath = localFilePath.Substring(_rootFolderPath.Length).Replace('\\', '/');
                    var remoteState = _dbContext.RemoteStates.FirstOrDefault(current => current.Path == remoteFilePath);

                    // conflict does not exist in database
                    // actions: add to database
                    if (localFilePath.Contains("(Conflicted Copy"))
                    {
                        lock (_dbContextLock)
                        {
                            if (!_dbContext.Conflicts.Any(conflict => conflict.FilePath == localFilePath))
                                _dbContext.Conflicts.Add(new Conflict() { FilePath = localFilePath });
                        }
                    }
                    // TODO: Originaldatei nicht hochladen, wenn Conflicted Copy Datei vorliegt!
                    // synchronize (upload)
                    else
                    {
                        var driveItem = await this.SyncLocalFile(localFilePath, remoteFilePath, remoteState);

                        if (driveItem != null)
                        {
                            var newRemoteState = new RemoteState()
                            {
                                Id = driveItem.Id,
                                Path = Path.Combine(driveItem.ParentReference.Path.Substring("/drive/root:".Length), driveItem.Name),
                                ETag = driveItem.ETag,
                                Size = driveItem.Size.Value,
                                Type = GraphItemType.File,
                                LastModified = driveItem.FileSystemInfo.LastModifiedDateTime.Value,
                            };

                            lock (_dbContextLock)
                            {
                                _dbContext.RemoteStates.Add(newRemoteState);
                            }
                        }
                    }
                });

                await Task.WhenAll(tasks);

                _dbContext.SaveChanges();
            });

            await this.ProcessConflicts();
        }

        // medium level
        private async Task<DriveItem> SyncLocalFile(string localFilePath, string remoteFilePath, RemoteState remoteState)
        {
            // file is not available remotely
            // actions: upload
            if (remoteState == null)
            {
                return await this.UploadFile(localFilePath, remoteFilePath);
            }

            return null;
        }

        private async Task SyncRemoteFile(RemoteState remoteState, string localFilePath, Uri downloadUri)
        {
            // file is not available locally
            // actions: download
            if (!File.Exists(localFilePath))
            {
                try
                {
                    await _webClient.DownloadFileTaskAsync(downloadUri, localFilePath);
                }
                catch (Exception)
                {
                    // TODO catch exception and request new URL
                    throw;
                }
            }
            // file is available locally and their hashes are equal
            // actions: ensure that time stamps become equal
            else if (this.GetHash(localFilePath) == remoteState.ETag)
            {
                var lastModified = remoteState.LastModified.UtcDateTime;

                if (File.GetLastWriteTimeUtc(localFilePath) != lastModified)
                    File.SetLastWriteTimeUtc(localFilePath, lastModified);
            }
            // file is available locally but their hashes are not equal
            // actions: - create a conflicted copy file
            //          - create a new entry in the 'conflicts' table
            else
            {
                var path = Path.GetDirectoryName(localFilePath);
                var name = Path.GetFileNameWithoutExtension(localFilePath);
                var extension = Path.GetExtension(localFilePath);
                var conflictedPath = $"{Path.Combine(path, name)} (Conflicted Copy {DateTime.Now.ToString("yyy-MM-dd HHmmss")})";

                if (!string.IsNullOrWhiteSpace(extension))
                    conflictedPath += $".{extension}";

                await _webClient.DownloadFileTaskAsync(downloadUri, conflictedPath);

                lock (_dbContextLock)
                {
                    _dbContext.Conflicts.Add(new Conflict() { FilePath = conflictedPath });
                    _dbContext.SaveChanges();
                }
            }
        }

        private void SyncRemoteFolder(string localPath)
        {
            // folder is not available locally
            // actions: create
            if (!Directory.Exists(localPath))
                Directory.CreateDirectory(localPath);
        }

        private async Task ProcessLocalDelta(Func<List<string>, Task> action)
        {
            var pageSize = 20;
            var deltaPage = new List<string>();
            var counter = 0;

            foreach (var localFilePath in DirectoryHelper.SafelyEnumerateFiles(_rootFolderPath, "*", SearchOption.AllDirectories)
                .Where(current => this.CheckPathAllowed(current)))
            {
                if (counter % pageSize == 0)
                {
                    await action?.Invoke(deltaPage);
                    deltaPage.Clear();
                }

                deltaPage.Add(localFilePath);

                counter = unchecked(counter + 1);
            }

            await action?.Invoke(deltaPage);
        }

        private async Task ProcessRemoteDelta(Func<IDriveItemDeltaCollectionPage, Task> action)
        {
            //var result = await _client.Subscriptions.Request().AddAsync(new Subscription()
            //{
            //    ChangeType = "updated,deleted",
            //    NotificationUrl = /* skipped */,
            //    ExpirationDateTime = DateTimeOffset.UtcNow.AddMinutes(10),
            //    Resource = "/me/drive/root",
            //}, token);

            // get delta
            bool isLast = false;
            IDriveItemDeltaCollectionPage deltaPage;

            while (true)
            {
                if (string.IsNullOrWhiteSpace(_token))
                    deltaPage = await _graphClient.Me.Drive.Root.Delta().Request().GetAsync();
                else
                    deltaPage = await _graphClient.Me.Drive.Root.Delta().Request(new List<Option> { new QueryOption("token", _token) }).GetAsync();

                // extract next token
                if (deltaPage.AdditionalData.ContainsKey("@odata.nextLink"))
                {
                    _token = deltaPage.AdditionalData["@odata.nextLink"].ToString().Split("=")[1];
                }
                else
                {
                    _token = deltaPage.AdditionalData["@odata.deltaLink"].ToString().Split("=")[1];
                    isLast = true;
                }

                await action?.Invoke(deltaPage);

                // exit while loop
                if (isLast)
                    break;
            }
        }

        private async Task ProcessConflicts()
        {
            // check if all conflicts still exist
            var conflictsToDelete = new List<Conflict>();

            foreach (var conflict in _dbContext.Conflicts)
            {
                // conflict file does not exist locally AND 
                // actions: remove from database
                if (!File.Exists(conflict.FilePath))
                    conflictsToDelete.Add(conflict);
                // conflict file does not exist locally
                // actions: remove from database
                else 
            }

            conflictsToDelete.ForEach(conflict => _dbContext.Conflicts.RemoveRange(conflictsToDelete));

            await _dbContext.SaveChangesAsync();
        }

        // low level
        private async Task<DriveItem> UploadFile(string localFilePath, string remoteFilePath)
        {
            var fileSystemInfo = new FileInfo(localFilePath);

            var graphFileSystemInfo = new Microsoft.Graph.FileSystemInfo()
            {
                CreatedDateTime = fileSystemInfo.CreationTimeUtc,
                LastAccessedDateTime = fileSystemInfo.LastAccessTimeUtc,
                LastModifiedDateTime = fileSystemInfo.LastWriteTimeUtc
            };

            DriveItem newDriveItem = null;

            using (var stream = File.OpenRead(localFilePath))
            {
                if (fileSystemInfo.Length <= 4 * 1024 * 1024) // file.Length <= 4 MB
                {
                    var driveItem = new DriveItem()
                    {
                        File = new Microsoft.Graph.File(),
                        FileSystemInfo = graphFileSystemInfo,
                        Name = Path.GetFileName(remoteFilePath)
                    };

                    newDriveItem = await _graphClient.UploadSmallFile4(driveItem, stream, remoteFilePath);
                }
                else
                {
                    var properties = new DriveItemUploadableProperties()
                    {
                        FileSystemInfo = graphFileSystemInfo
                    };

                    newDriveItem = await _graphClient.OneDriveUploadLargeFile(stream, properties, remoteFilePath);
                }
            }

            return newDriveItem;
        }

        private string GetHash(string filePath)
        {
            var _hashAlgorithm = new QuickXorHash();

            using (FileStream stream = File.OpenRead(filePath))
            {
                return Convert.ToBase64String(_hashAlgorithm.ComputeHash(stream));
            }
        }

        private bool CheckPathAllowed(string filePath)
        {
            return true;
        }
    }
}
