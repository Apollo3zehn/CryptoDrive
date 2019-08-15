using CryptoDrive.Extensions;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
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
        private Regex _regex_conflict;
        private Regex _regex_replace;

        private object _dbContextLock;

        public OneDriveSynchronizer(GraphServiceClient graphClient, OneDriveContext dbContext)
        {
            _graphClient = graphClient;
            _dbContext = dbContext;

            _rootFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CryptoDrive");
            _webClient = new WebClient();
            _dbContextLock = new object();
            _regex_conflict = new Regex(@".*\s\(Conflicted Copy [0-9]{4}-[0-9]{2}-[0-9]{2}\s[0-9]{6}\)");
            _regex_replace = new Regex(@"\s\(Conflicted Copy [0-9]{4}-[0-9]{2}-[0-9]{2}\s[0-9]{6}\)");
        }

        // high level
        public async Task SynchronizeTheUniverse(GraphServiceClient graphClient, OneDriveContext dbContext)
        {
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
                    var remoteState = this.GetRemoteStateFromDriveItem(driveItem);

                    // synchronize (download)
                    if (remoteState.Type == GraphItemType.File)
                    {
                        var downloadUri = new Uri(driveItem.AdditionalData["@microsoft.graph.downloadUrl"].ToString());
                        await this.SyncRemoteFile(remoteState, downloadUri);
                    }
                    else if (remoteState.Type == GraphItemType.Folder)
                    {
                        this.SyncRemoteFolder(remoteState);
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
                    // if file is marked as conflicted copy
                    if (_regex_conflict.IsMatch(localFilePath))
                    {
                        this.EnsureConflictByConflictFile(conflictFilePath: localFilePath);
                    }
                    // synchronize (upload)
                    else
                    {
                        var remoteFilePath = localFilePath.Substring(_rootFolderPath.Length).Replace('\\', '/');
                        var remoteState = _dbContext.RemoteStates.FirstOrDefault(current => current.Path == remoteFilePath);
                        var driveItem = await this.SyncLocalFile(localFilePath, remoteFilePath, remoteState);

                        if (driveItem != null)
                        {
                            lock (_dbContextLock)
                            {
                                _dbContext.RemoteStates.Add(this.GetRemoteStateFromDriveItem(driveItem));
                            }
                        }
                    }
                });

                await Task.WhenAll(tasks);

                _dbContext.SaveChanges();
            });

            await this.CheckConflicts();
        }

        // medium level
        private async Task SyncRemoteFile(RemoteState remoteState, Uri downloadUri)
        {
            var localFilePath = Path.Combine(_rootFolderPath, remoteState.Path);

            // file is available locally
            if (File.Exists(localFilePath))
            {
                // the last modified dates are equal
                // actions: do nothing
                if (File.GetLastWriteTimeUtc(localFilePath) == remoteState.LastModified.UtcDateTime)
                {
                    // do nothing
                }
                // the last modified dates are not equal
                // actions: - create a conflicted copy file
                //          - create a new entry in the 'conflicts' table
                else
                {
                    await this.EnsureConflict(remoteState, downloadUri);
                }
            }
            // file is not available locally
            // actions: download
            else
            {
                try
                {
                    await _webClient.DownloadFileTaskAsync(downloadUri, localFilePath);
                }
                // retry if download link has expired
                catch (Exception)
                {
                    var refreshedDownloadUri = new Uri(await _graphClient.GetDownloadUrlAsync(remoteState.Id));
                    await _webClient.DownloadFileTaskAsync(refreshedDownloadUri, localFilePath);
                }
            }
        }

        private void SyncRemoteFolder(RemoteState remoteState)
        {
            var localFolderPath = Path.Combine(_rootFolderPath, remoteState.Path);

            // folder is not available locally
            // actions: create
            if (!Directory.Exists(localFolderPath))
                Directory.CreateDirectory(localFolderPath);
        }

        private async Task<DriveItem> SyncLocalFile(string localFilePath, string remoteFilePath, RemoteState remoteState)
        {
            // file is marked as conflicted
            // action: do nothing, it will be handled by "CheckConflicts" later
            if (_dbContext.Conflicts.Any(conflict => conflict.OriginalFilePath == localFilePath))
            {
                // do nothing
            }
            // file is not part of any known conflicts
            else
            {
                // file is available remotely
                if (remoteState != null)
                {
                    // last modified times are equal
                    // actions: do nothing
                    if (File.GetLastWriteTimeUtc(localFilePath) == remoteState.LastModified)
                    {
                        // do nothing
                    }
                    // last modified times are not equal
                    // actions: create conflict
                    else
                    {
                        await this.EnsureConflict(remoteState);
                    }
                }
                // file is not available remotely
                // actions: upload
                else // 
                {
                    return await this.UploadFile(localFilePath, remoteFilePath);
                }
            }

            return null;
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

        private async Task CheckConflicts()
        {
            var resolvedConflicts = new List<Conflict>();

            foreach (var conflict in _dbContext.Conflicts)
            {
                if (await this.CheckConflict(conflict))
                    resolvedConflicts.Add(conflict);
            }

            _dbContext.Conflicts.RemoveRange(resolvedConflicts);
            _dbContext.SaveChanges();
        }

        private async Task<bool> CheckConflict(Conflict conflict)
        {
            var remoteFilePath = conflict.OriginalFilePath.Substring(_rootFolderPath.Length).Replace('\\', '/');
            var remoteItem = _dbContext.RemoteStates.FirstOrDefault(current => current.Path == remoteFilePath);

            // original file exists locally
            if (File.Exists(conflict.OriginalFilePath))
            {
                // conflict file exists locally
                // actions: do nothing - user must delete or rename conflict file manually
                if (File.Exists(conflict.ConflictFilePath))
                {
                    // do nothing
                }
                // conflict file does not exist locally, i.e. conflict is solved
                else
                {
                    // remote file is tracked in database
                    if (remoteItem != null)
                    {
                        // hashes are equal
                        // actions: do nothing
                        if (this.GetHash(conflict.OriginalFilePath) == remoteItem.ETag)
                        {
                            // do nothing
                        }
                        // actions: upload file and replace remote version
                        else
                        {
                            var driveItem = await this.UploadFile(conflict.OriginalFilePath, remoteFilePath);
                            _dbContext.RemoteStates.Add(this.GetRemoteStateFromDriveItem(driveItem));
                        }
                    }
                    // remote file is not tracked in database (e.g. upload failed previously)
                    // actions: upload file
                    else
                    {
                        var driveItem = await this.UploadFile(conflict.OriginalFilePath, remoteFilePath);
                        _dbContext.RemoteStates.Add(this.GetRemoteStateFromDriveItem(driveItem));
                    }

                    return true;
                }
            }
            // original file does not exist locally
            // actions: do nothing - user must delete or rename conflict file manually
            else
            {
                // do nothing
            }

            return false;
        }

        // low level
        private async Task EnsureConflict(RemoteState remoteState, Uri downloadUri = null)
        {
            var localFilePath = Path.Combine(_rootFolderPath, remoteState.Path);
            var conflictFilePath = this.ToConflictFilePath(localFilePath, remoteState.LastModified);

            // conflict file does not exist
            // actions: download file
            if (!File.Exists(conflictFilePath))
            {
                if (downloadUri == null)
                    downloadUri = new Uri(await _graphClient.GetDownloadUrlAsync(remoteState.Id));

                await _webClient.DownloadFileTaskAsync(downloadUri, conflictFilePath);
            }

            this.EnsureConflictByConflictFile(conflictFilePath);
        }

        private void EnsureConflictByConflictFile(string conflictFilePath)
        {
            lock (_dbContextLock)
            {
                var conflict = _dbContext.Conflicts.FirstOrDefault(current => current.ConflictFilePath == conflictFilePath);

                // conflict does not exist in database
                // actions: add new entry
                if (conflict == null)
                {
                    var originalFilePath = _regex_replace.Replace(conflictFilePath, string.Empty);

                    conflict = new Conflict()
                    {
                        OriginalFilePath = originalFilePath,
                        ConflictFilePath = conflictFilePath
                    };

                    _dbContext.Conflicts.Add(conflict);
                }

                _dbContext.SaveChanges();
            }
        }

        private string ToConflictFilePath(string filePath, DateTimeOffset lastModified)
        {
            var path = Path.GetDirectoryName(filePath);
            var name = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);
            var conflictedFilePath = $"{Path.Combine(path, name)} (Conflicted Copy {lastModified.ToString("yyy-MM-dd HHmmss")})";

            if (!string.IsNullOrWhiteSpace(extension))
                conflictedFilePath += $".{extension}";

            return conflictedFilePath;
        }

        private RemoteState GetRemoteStateFromDriveItem(DriveItem driveItem)
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
                Id = driveItem.Id,
                Path = Path.Combine(driveItem.ParentReference.Path.Substring("/drive/root:".Length), driveItem.Name),
                ETag = driveItem.ETag,
                Size = driveItem.Size.Value,
                Type = type,
                LastModified = driveItem.FileSystemInfo.LastModifiedDateTime.Value,
            };
        }

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

                    newDriveItem = await _graphClient.UploadSmallFile4Async(driveItem, stream);
                }
                else
                {
                    var properties = new DriveItemUploadableProperties()
                    {
                        FileSystemInfo = graphFileSystemInfo
                    };

                    newDriveItem = await _graphClient.OneDriveUploadLargeFileAsync(stream, properties, remoteFilePath);
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
