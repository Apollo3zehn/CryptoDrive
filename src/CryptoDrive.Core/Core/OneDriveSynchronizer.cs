using CryptoDrive.Extensions;
using Microsoft.Extensions.Logging;
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

namespace CryptoDrive.Core
{
    public class OneDriveSynchronizer
    {
        private string _rootFolderPath;

        private CryptoDriveDbContext _dbContext;
        private Regex _regex_conflict;
        private Regex _regex_replace;

        private ILogger _logger;
        private IOneDriveClient _oneDriveClient;

        private object _dbContextLock;

        public OneDriveSynchronizer(string rootFolderPath, IOneDriveClient oneDriveClient, CryptoDriveDbContext dbContext, ILogger logger)
        {
            _rootFolderPath = rootFolderPath;
            _oneDriveClient = oneDriveClient;
            _dbContext = dbContext;
            _logger = logger;

            _dbContextLock = new object();
            _regex_conflict = new Regex(@".*\s\(Conflicted Copy [0-9]{4}-[0-9]{2}-[0-9]{2}\s[0-9]{6}\)");
            _regex_replace = new Regex(@"\s\(Conflicted Copy [0-9]{4}-[0-9]{2}-[0-9]{2}\s[0-9]{6}\)");
        }

        // high level
        public async Task SynchronizeTheUniverse()
        {
            await this.BuildIndex();
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
                        using (_logger.BeginScope(new Dictionary<string, object>
                        {
                            ["FilePath"] = remoteState.Path
                        }))
                        {
                            var downloadUri = new Uri(driveItem.AdditionalData[CryptoDriveConstants.DownloadUrl].ToString());
                            await this.SyncRemoteFile(remoteState, downloadUri);
                        }
                    }
                    else if (remoteState.Type == GraphItemType.Folder)
                    {
                        using (_logger.BeginScope(new Dictionary<string, object>
                        {
                            ["FolderPath"] = remoteState.Path
                        }))
                        {
                            this.SyncRemoteFolder(remoteState);
                        }
                    }

                    return remoteState;
                }).ToList();

                await Task.WhenAll(tasks);
                var remoteStates = tasks.Select(task => task.Result).Where(state => state.Path != "root");

                _dbContext.RemoteStates.AddRange(remoteStates);
                await _dbContext.SaveChangesAsync();
            });

            await this.ProcessLocalDelta(async deltaPage =>
            {
                var tasks = deltaPage.Select(async absoluteFilePath =>
                {
                    // if file is marked as conflicted copy
                    if (_regex_conflict.IsMatch(absoluteFilePath))
                    {
                        this.EnsureConflictByConflictFile(conflictFilePath: absoluteFilePath);
                    }
                    // synchronize (upload)
                    else
                    {
                        var relativeFilePath = absoluteFilePath.ToRelativePath(_rootFolderPath);
                        var remoteState = _dbContext.RemoteStates.FirstOrDefault(current => current.Path == relativeFilePath);

                        using (_logger.BeginScope(new Dictionary<string, object>
                        {
                            ["FilePath"] = relativeFilePath
                        }))
                        {
                            var driveItem = await this.SyncLocalFile(absoluteFilePath, relativeFilePath, remoteState);
                            
                            if (driveItem != null)
                            {
                                lock (_dbContextLock)
                                {
                                    _dbContext.RemoteStates.Add(this.GetRemoteStateFromDriveItem(driveItem));
                                }
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
            var absoluteFilePath = Path.Combine(_rootFolderPath, remoteState.Path);

            // file is available locally
            if (File.Exists(absoluteFilePath))
            {
                _logger.LogTrace($"Local file exists.");

                // the last modified dates are equal
                // actions: do nothing
                if (File.GetLastWriteTimeUtc(absoluteFilePath) == remoteState.LastModified)
                {
                    _logger.LogTrace($"Local file is unchanged. Action(s): do nothing.");
                }
                // the last modified dates are not equal
                else
                {
                    _logger.LogTrace($"Local file has been modified.");

                    await this.EnsureConflict(remoteState, downloadUri);
                }
            }
            // file is not available locally
            // actions: download
            else
            {
                var webClient = new WebClient();

                try
                {
                    _logger.LogTrace($"Local file does not exist. Action(s): download file.");
                    await webClient.DownloadFileTaskAsync(downloadUri, absoluteFilePath);
                }
                // retry if download link has expired
                catch (Exception)
                {
                    var refreshedDownloadUri = new Uri(await _oneDriveClient.GetDownloadUrlAsync(remoteState.Id));
                    await webClient.DownloadFileTaskAsync(refreshedDownloadUri, absoluteFilePath);
                }

                File.SetLastWriteTimeUtc(absoluteFilePath, remoteState.LastModified);
            }
        }

        private void SyncRemoteFolder(RemoteState remoteState)
        {
            var localFolderPath = Path.Combine(_rootFolderPath, remoteState.Path);

            // <no condition>
            // actions: try to create directory
            _logger.LogDebug($"Action(s): Try to creating directory.");
            Directory.CreateDirectory(localFolderPath);
        }

        private async Task<DriveItem> SyncLocalFile(string absoluteFilePath, string relativeFilePath, RemoteState remoteState)
        {
            // file is marked as conflicted
            // action: do nothing, it will be handled by "CheckConflicts" later
            if (_dbContext.Conflicts.Any(conflict => conflict.OriginalFilePath == absoluteFilePath))
            {
                _logger.LogTrace($"File is tracked as conflict. Action(s): do nothing.");
            }
            // file is not part of any known conflicts
            else
            {
                // file is available remotely
                if (remoteState != null)
                {
                    _logger.LogTrace($"File is available on remote drive.");

                    // last modified times are equal
                    // actions: do nothing
                    if (File.GetLastWriteTimeUtc(absoluteFilePath) == remoteState.LastModified)
                    {
                        _logger.LogTrace($"File is unchanged. Action(s): do nothing.");
                    }
                    // last modified times are not equal
                    else
                    {
                        _logger.LogTrace($"File has been modified.");
                        await this.EnsureConflict(remoteState);
                    }
                }
                // file is not available remotely
                // actions: upload
                else
                {
                    _logger.LogTrace($"File is not available on remote drive. Action(s): upload.");
                    return await _oneDriveClient.UploadFileAsync(absoluteFilePath, relativeFilePath);
                }
            }

            return null;
        }

        private async Task ProcessLocalDelta(Func<List<string>, Task> action)
        {
            var counter = 0;
            var pageCounter = 0;
            var pageSize = 20;
            var deltaPage = new List<string>();

            foreach (var localFilePath in DirectoryHelper.SafelyEnumerateFiles(_rootFolderPath, "*", SearchOption.AllDirectories)
                .Where(current => this.CheckPathAllowed(current)))
            {
                if ((counter + 1) % pageSize == 0)
                {
                    using (_logger.BeginScope(new Dictionary<string, object>
                    {
                        ["LocalDeltaPage"] = pageCounter
                    }))
                    {
                        await action?.Invoke(deltaPage);
                        pageCounter++;
                    }

                    deltaPage.Clear();
                }

                deltaPage.Add(localFilePath);
                counter = unchecked(counter + 1);
            }

            // process remaining files
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["LocalDeltaPage"] = pageCounter
            }))
            {
                await action?.Invoke(deltaPage);
            }
        }

        private async Task ProcessRemoteDelta(Func<List<DriveItem>, Task> action)
        {
            //var result = await _client.Subscriptions.Request().AddAsync(new Subscription()
            //{
            //    ChangeType = "updated,deleted",
            //    NotificationUrl = /* skipped */,
            //    ExpirationDateTime = DateTimeOffset.UtcNow.AddMinutes(10),
            //    Resource = "/me/drive/root",
            //}, token);

            // get delta
            var pageCounter = 0;

            while (true)
            {
                using (_logger.BeginScope(new Dictionary<string, object>
                {
                    ["RemoteDeltaPage"] = pageCounter
                }))
                {
                    (var deltaPage, var isLast) = await _oneDriveClient.GetDeltaPageAsync();

                    await action?.Invoke(deltaPage);
                    pageCounter++;

                    // exit while loop
                    if (isLast)
                        break;
                }
            }
        }

        private async Task CheckConflicts()
        {
            var resolvedConflicts = new List<Conflict>();

            foreach (var conflict in _dbContext.Conflicts)
            {
                using (_logger.BeginScope(new Dictionary<string, object>
                {
                    [nameof(conflict.ConflictFilePath)] = conflict.ConflictFilePath,
                    [nameof(conflict.OriginalFilePath)] = conflict.OriginalFilePath
                }))
                {
                    if (await this.CheckConflict(conflict))
                    {
                        _logger.LogTrace($"Conflict has been resolved.");
                        resolvedConflicts.Add(conflict);
                    }
                }
            }

            _dbContext.Conflicts.RemoveRange(resolvedConflicts);
            _dbContext.SaveChanges();
        }

        private async Task<bool> CheckConflict(Conflict conflict)
        {
            var relativeFilePath = conflict.OriginalFilePath.ToRelativePath(_rootFolderPath);
            var remoteItem = _dbContext.RemoteStates.FirstOrDefault(current => current.Path == relativeFilePath);

            // original file exists locally
            if (File.Exists(conflict.OriginalFilePath))
            {
                _logger.LogTrace($"Original file exists locally.");

                // conflict file exists locally
                // actions: do nothing - user must delete or rename conflict file manually
                if (File.Exists(conflict.ConflictFilePath))
                {
                    _logger.LogTrace($"Conflict file exists locally. Action(s): do nothing.");
                }
                // conflict file does not exist locally, i.e. conflict is solved
                else
                {
                    _logger.LogTrace($"Conflict file does not exist locally.");

                    // remote file is tracked in database
                    if (remoteItem != null)
                    {
                        _logger.LogTrace($"Remote file is tracked in database.");

                        // hashes are equal
                        // actions: do nothing
                        if (this.GetHash(conflict.OriginalFilePath) == remoteItem.ETag)
                        {
                            _logger.LogTrace($"The file is unchanged. Action(s): do nothing.");
                        }
                        // actions: upload file and replace remote version
                        else
                        {
                            _logger.LogTrace($"The file is modified. Action(s): upload file.");

                            var driveItem = await _oneDriveClient.UploadFileAsync(conflict.OriginalFilePath, relativeFilePath);
                            _dbContext.RemoteStates.Add(this.GetRemoteStateFromDriveItem(driveItem));
                        }
                    }
                    // remote file is not tracked in database (e.g. upload failed previously)
                    // actions: upload file
                    else
                    {
                        _logger.LogTrace($"Remote file is not tracked in database. Action(s): upload file.");

                        var driveItem = await _oneDriveClient.UploadFileAsync(conflict.OriginalFilePath, relativeFilePath);
                        _dbContext.RemoteStates.Add(this.GetRemoteStateFromDriveItem(driveItem));
                    }

                    return true;
                }
            }
            // original file does not exist locally
            // actions: do nothing - user must delete or rename conflict file manually
            else
            {
                _logger.LogTrace($"Original file does not exist locally. Action(s): do nothing.");
            }

            return false;
        }

        // low level
        private async Task EnsureConflict(RemoteState remoteState, Uri downloadUri = null)
        {
            var localFilePath = Path.Combine(_rootFolderPath, remoteState.Path);
            var conflictFilePath = localFilePath.ToConflictFilePath(remoteState.LastModified);

            // conflict file does not exist
            // actions: download file
            if (!File.Exists(conflictFilePath))
            {
                _logger.LogTrace($"Conflict file does not exist locally. Actions(s): download file.");

                if (downloadUri == null)
                {
                    _logger.LogTrace($"Download URI is null. Action(s): Request new download URI.");
                    downloadUri = new Uri(await _oneDriveClient.GetDownloadUrlAsync(remoteState.Id));
                }

                await (new WebClient()).DownloadFileTaskAsync(downloadUri, conflictFilePath);
                File.SetLastWriteTimeUtc(conflictFilePath, remoteState.LastModified);
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
                    _logger.LogTrace($"Conflict entry does not exist. Actions(s): Add new entry.");
                    var originalFilePath = _regex_replace.Replace(conflictFilePath, string.Empty);

                    conflict = new Conflict()
                    {
                        OriginalFilePath = originalFilePath,
                        ConflictFilePath = conflictFilePath
                    };

                    _dbContext.Conflicts.Add(conflict);
                }
                else
                {
                    _logger.LogTrace($"Conflict entry already exists. Actions(s): do nothing.");
                }

                _dbContext.SaveChanges();
            }
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
                Path = Path.Combine(driveItem.ParentReference.Path.Substring(CryptoDriveConstants.PathPrefix.Length), driveItem.Name),
                ETag = driveItem.ETag,
                Size = driveItem.Size.Value,
                Type = type,
                LastModified = driveItem.FileSystemInfo.LastModifiedDateTime.Value.DateTime,
            };
        }

        private string GetHash(string filePath)
        {
            var _hashAlgorithm = new QuickXorHash();

            using (var stream = File.OpenRead(filePath))
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
