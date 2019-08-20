using CryptoDrive.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDrive.Core
{
    public class CryptoDriveSyncEngine
    {
        private CryptoDriveDbContext _dbContext;
        private Regex _regex_conflict;
        private Regex _regex_replace;

        private ILogger _logger;
        private IDriveProxy _remoteDrive;
        private IDriveProxy _localDrive;

        private object _dbContextLock;

        public CryptoDriveSyncEngine(IDriveProxy remoteDrive, IDriveProxy localDrive, CryptoDriveDbContext dbContext, ILogger logger)
        {
            _remoteDrive = remoteDrive;
            _localDrive = localDrive;
            _dbContext = dbContext;
            _logger = logger;

            _dbContextLock = new object();
            _regex_conflict = new Regex(@".*\s\(Conflicted Copy [0-9]{4}-[0-9]{2}-[0-9]{2}\s[0-9]{6}\)");
            _regex_replace = new Regex(@"\s\(Conflicted Copy [0-9]{4}-[0-9]{2}-[0-9]{2}\s[0-9]{6}\)");
        }

        // high level
        public async Task SynchronizeTheUniverse()
        {
            // prepare database
            _dbContext.Database.EnsureCreated();

            await _remoteDrive.ProcessDelta(async deltaPage => await this.SyncChanges(deltaPage, isLocal: false));
            await _localDrive.ProcessDelta(async deltaPage => await this.SyncChanges(deltaPage, isLocal: true));

            await this.CheckConflicts();
        }

        private async Task SyncChanges(List<DriveItem> deltaPage, bool isLocal)
        {
            foreach (var newDriveItem in deltaPage)
            {
                using (_logger.BeginScope(new Dictionary<string, object>
                {
                    ["FilePath"] = newDriveItem.GetPath()
                }))
                {
                    // if file is marked as conflicted copy
                    if (isLocal && _regex_conflict.IsMatch(newDriveItem.Name))
                    {
                        this.EnsureConflictByConflictFile(conflictFilePath: newDriveItem.GetPath());
                    }
                    // proceed
                    else
                    {
                        RemoteState oldRemoteState;
                        DriveItem oldDriveItem;
                        DriveItem updatedDriveItem;

                        // find old remote state item
                        lock (_dbContextLock)
                        {
                            if (isLocal)
                                oldRemoteState = _dbContext.RemoteStates.FirstOrDefault(current => current.Path == newDriveItem.GetPath());
                            else
                                oldRemoteState = _dbContext.RemoteStates.FirstOrDefault(current => current.Id == newDriveItem.Id);

                            oldDriveItem = oldRemoteState?.ToDriveItem();
                        }

                        // synchronize
                        if (isLocal)
                            updatedDriveItem = await this.SyncLocal(oldDriveItem, newDriveItem);
                        else
                            updatedDriveItem = await this.SyncRemote(oldDriveItem, newDriveItem);

                        // update database
                        if (updatedDriveItem != null)
                        {
                            lock (_dbContextLock)
                            {
                                if (updatedDriveItem.GetPath() == "root")
                                    return;

                                if (oldDriveItem == null)
                                    _dbContext.RemoteStates.Add(updatedDriveItem.ToRemoteState());
                                else
                                    _dbContext.Entry(oldRemoteState).CurrentValues.SetValues(updatedDriveItem.ToRemoteState());

                                _dbContext.SaveChanges();
                            }
                        }
                    }
                }
            }
        }

        private async Task FollowUp(CancellationToken cts)
        {
            while (!cts.IsCancellationRequested)
            {
                //await this.ProcessChanges();
                await Task.Delay(TimeSpan.FromSeconds(10), cts);
            }
        }

        // medium level
        private async Task<DriveItem> SyncRemote(DriveItem oldDriveItem, DriveItem newDriveItem)
        {
            string itemName1;
            string itemName2;
            DriveItem driveItem = null;

            switch (newDriveItem.Type())
            {
                case GraphItemType.Folder:
                    itemName1 = "Folder"; itemName2 = "folder";
                    break;
                case GraphItemType.File:
                    itemName1 = "File"; itemName2 = "file";
                    break;
                case GraphItemType.RemoteItem:
                    return null;
                default:
                    throw new ArgumentException();
            }

            // item was deleted on remote drive
            // actions: delete item on local drive
            if (newDriveItem.IsDeleted())
            {
                _logger.LogInformation($"{itemName1} was deleted on remote drive. Action(s): Delete {itemName2} on local drive.");

                if (await _localDrive.ExistsAsync(newDriveItem))
                    await _localDrive.DeleteAsync(newDriveItem);
                else
                    _logger.LogWarning($"Cannot delete local {itemName2} because it does not exist.");
            }

            // item was renamed / moved on remote drive
            // actions: rename / move item on local drive
            else if (oldDriveItem != null && oldDriveItem.GetPath() != newDriveItem.GetPath())
            {
                _logger.LogInformation($"{itemName1} was renamed / moved on remote drive. Action(s): Rename / move {itemName2} on local drive.");

                if (await _localDrive.ExistsAsync(newDriveItem))
                    _logger.LogWarning($"Cannot delete move {itemName2} because the target {itemName2} already exists.");
                else
                    driveItem = await _localDrive.MoveAsync(oldDriveItem, newDriveItem);
            }

            // new item was created on remote drive
            // actions: create on local drive
            else
            {
                _logger.LogInformation($"New {itemName2} was created on remote drive. Action(s): Create {itemName2} on local drive.");
                driveItem = await this.TransferFile(_remoteDrive, _localDrive, newDriveItem);
            }

            return driveItem;
        }

        private async Task<DriveItem> SyncLocal(DriveItem oldDriveItem, DriveItem newDriveItem)
        {
            // file is tracked as conflict
            // action: do nothing, it will be handled by "CheckConflicts" later
            if (_dbContext.Conflicts.Any(conflict => conflict.OriginalFilePath == newDriveItem.GetPath()))
            {
                _logger.LogDebug($"File is tracked as conflict. Action(s): do nothing.");
            }

            // file is not part of any known conflicts
            else
            {
                // file is not available on remote drive
                if (oldDriveItem == null)
                {
                    switch (newDriveItem.GetChangeType(oldDriveItem))
                    {
                        case WatcherChangeTypes.Changed:
                        case WatcherChangeTypes.Created:
                        case WatcherChangeTypes.Renamed:
                            return await this.TransferFile(_localDrive, _remoteDrive, newDriveItem);

                        // cannot happen
                        case WatcherChangeTypes.Deleted:
                            break;

                        // do nothing
                        default:
                            break;
                    }
                }

                // file is available on remote drive
                else
                {
                    switch (newDriveItem.GetChangeType(oldDriveItem))
                    {
                        case WatcherChangeTypes.Changed:

                            // change

                            break;

                        case WatcherChangeTypes.Created:

                            // upload

                            break;

                        case WatcherChangeTypes.Deleted:

                            // delete

                            break;

                        case WatcherChangeTypes.Renamed:

                            // rename

                            break;

                        default:
                            // do nothing
                            break;
                    }
                }
            }

            return null;
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
                        _logger.LogDebug($"Conflict was resolved.");
                        resolvedConflicts.Add(conflict);
                    }
                }
            }

            _dbContext.Conflicts.RemoveRange(resolvedConflicts);
            _dbContext.SaveChanges();
        }

        private async Task<bool> CheckConflict(Conflict conflict)
        {
            var remoteItem = _dbContext.RemoteStates.FirstOrDefault(current => current.Path == conflict.OriginalFilePath);
            var originalDriveItem = conflict.OriginalFilePath.ToDriveItem();

            // original file exists locally
            if (await _localDrive.ExistsAsync(originalDriveItem))
            {
                _logger.LogDebug($"Original file exists locally.");
                var conflictDriveItem = conflict.ConflictFilePath.ToDriveItem();

                // conflict file exists locally
                // actions: do nothing - user must delete or rename conflict file manually
                if (await _localDrive.ExistsAsync(conflictDriveItem))
                {
                    _logger.LogDebug($"Conflict file exists locally. Action(s): do nothing.");
                }

                // conflict file does not exist locally, i.e. conflict is solved
                else
                {
                    _logger.LogDebug($"Conflict file does not exist locally.");

                    // remote file is tracked in database
                    if (remoteItem != null)
                    {
                        _logger.LogDebug($"Remote file is tracked in database.");

                        // hashes are equal
                        // actions: do nothing
                        if (await _localDrive.GetHashAsync(originalDriveItem) == remoteItem.ETag)
                        {
                            _logger.LogDebug($"File is unchanged. Action(s): do nothing.");
                        }

                        // actions: upload file and replace remote version
                        else
                        {
                            _logger.LogInformation($"File was modified. Action(s): upload and replace file.");

                            originalDriveItem = await _localDrive.ToFullDriveItem(originalDriveItem);
                            var driveItem = await _remoteDrive.CreateOrUpdateAsync(originalDriveItem);
                            _dbContext.RemoteStates.Add(driveItem.ToRemoteState());
                        }
                    }

                    // remote file is not tracked in database (e.g. upload failed previously)
                    // actions: upload file
                    else
                    {
                        _logger.LogInformation($"Remote file is not tracked in database. Action(s): upload file.");

                        originalDriveItem = await _localDrive.ToFullDriveItem(originalDriveItem);
                        var driveItem = await _remoteDrive.CreateOrUpdateAsync(originalDriveItem);
                        _dbContext.RemoteStates.Add(driveItem.ToRemoteState());
                    }

                    return true;
                }
            }

            // original file does not exist locally
            // actions: do nothing - user must delete or rename conflict file manually
            else
            {
                _logger.LogDebug($"Original file does not exist locally. Action(s): do nothing.");
            }

            return false;
        }

        // low level
        private async Task<DriveItem> TransferFile(IDriveProxy sourceDrive, IDriveProxy targetDrive, DriveItem driveItem)
        {
            // file exists on target drive
            if (await targetDrive.ExistsAsync(driveItem))
            {
                // file is unchanged on target drive
                // actions: do nothing
                if (await targetDrive.GetLastWriteTimeUtcAsync(driveItem) == driveItem.LastModified())
                    _logger.LogDebug($"File is unchanged on drive '{targetDrive.Name}'. Action(s): do nothing.");

                // file was modified on target drive
                else
                    await this.EnsureConflict(sourceDrive, targetDrive, driveItem);
            }
            // file does not exist on target drive
            // actions: transfer file
            else
            {
                _logger.LogInformation($"File is not available on drive '{targetDrive.Name}'. Action(s): transfer file.");
                return await this.InternalTransferFile(sourceDrive, targetDrive, driveItem);
            }

            return null;
        }

        private async Task<DriveItem> InternalTransferFile(IDriveProxy sourceDrive, IDriveProxy targetDrive, DriveItem driveItem)
        {
            DriveItem newDriveItem;

            try
            {
                newDriveItem = await targetDrive.CreateOrUpdateAsync(driveItem);
            }
            // retry if download link has expired
            catch (Exception)
            {
                _logger.LogWarning($"Download URI is null or has expired, requesting new one.");
                driveItem.SetUri(await sourceDrive.GetDownloadUriAsync(driveItem));
                newDriveItem = await targetDrive.CreateOrUpdateAsync(driveItem);
            }

            await targetDrive.SetLastWriteTimeUtcAsync(driveItem);
            return newDriveItem;
        }

        private async Task EnsureConflict(IDriveProxy sourceDrive, IDriveProxy targetDrive, DriveItem driveItem)
        {
            var conflictDriveItem = driveItem.ToConflict();

            // conflict file does not exist
            // actions: download file
            if (!await _localDrive.ExistsAsync(conflictDriveItem))
            {
                _logger.LogInformation($"Conflict file does not exist on drive '{targetDrive.Name}'. Actions(s): transfer file.");
                await this.InternalTransferFile(sourceDrive, targetDrive, conflictDriveItem);
            }

            this.EnsureConflictByConflictFile(conflictDriveItem.GetPath());
        }

        private void EnsureConflictByConflictFile(string conflictFilePath)
        {
            lock (_dbContextLock)
            {
                var conflict = _dbContext.Conflicts.FirstOrDefault(current => current.ConflictFilePath == conflictFilePath);

                // conflict does not exist in database
                // actions: add new entity
                if (conflict == null)
                {
                    _logger.LogDebug($"Conflict entity does not exist. Actions(s): Add new entity.");
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
                    _logger.LogDebug($"Conflict entity already exists. Actions(s): do nothing.");
                }

                _dbContext.SaveChanges();
            }
        }
    }
}
