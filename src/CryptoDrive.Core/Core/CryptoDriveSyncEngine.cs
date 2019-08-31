using CryptoDrive.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDrive.Core
{
    public class CryptoDriveSyncEngine : IDisposable
    {
        #region Events

        public event EventHandler<SyncCompletedEventArgs> SyncCompleted;

        #endregion

        #region Fields

        private CryptoDriveDbContext _dbContext;
        private Regex _regex_conflict;
        private Regex _regex_replace;

        private ILogger _logger;
        private IDriveProxy _remoteDrive;
        private IDriveProxy _localDrive;

        private SyncMode _syncMode;

        private CancellationTokenSource _cts;
        private ManualResetEventSlim _manualReset;
        private ConcurrentQueue<string> _changesQueue;
        private Task _watchTask;

        private EventHandler<string> _handler;

        private int _syncId;

        #endregion

        #region Constructors

        public CryptoDriveSyncEngine(IDriveProxy remoteDrive,
                                     IDriveProxy localDrive,
                                     CryptoDriveDbContext dbContext,
                                     SyncMode syncMode,
                                     ILogger logger)
        {
            _remoteDrive = remoteDrive;
            _localDrive = localDrive;
            _dbContext = dbContext;
            _syncMode = syncMode;
            _logger = logger;

            _syncId = 0;

            _regex_conflict = new Regex(@".*\s\(Conflicted Copy [0-9]{4}-[0-9]{2}-[0-9]{2}\s[0-9]{6}\)");
            _regex_replace = new Regex(@"\s\(Conflicted Copy [0-9]{4}-[0-9]{2}-[0-9]{2}\s[0-9]{6}\)");

            // changes
            _cts = new CancellationTokenSource();
            _manualReset = new ManualResetEventSlim();
            _changesQueue = new ConcurrentQueue<string>();

            // handler
            _handler = (sender, e) =>
            {
                _changesQueue.Enqueue(e);
                _manualReset.Set();
            };
        }

        #endregion

        #region Properties

        public bool IsEnabled { get; private set; }

        #endregion

        #region Methods

        // public

        public void Start(string folderPath = "")
        {
            // if already running, throw exception
            if (this.IsEnabled)
                throw new InvalidOperationException("The sync engine is already started.");

            // sync engine and change tracking is now enabled
            this.IsEnabled = true;

            // create watch task
            _watchTask = Task.Run(() => this.WatchForChanges(folderPath));

            // add folder change event handler to local drive
            _localDrive.FolderChanged += _handler;

            _logger.LogInformation("Sync engine started.");
        }

        public void Stop()
        {
            if (this.IsEnabled)
            {
                // avoid new entries to changes queue
                _localDrive.FolderChanged -= _handler;

                // clear changes queue
                _changesQueue.Clear();

                // avoid possible deadlock due to waiting for manual reset event signal
                _manualReset.Set();

                // disable while loop
                this.IsEnabled = false;

                // clear database since from now on we miss events and so we need to re-sync 
                // everything the next time the engine is started
                this.ClearDatabase();
            }

            _logger.LogInformation("Sync engine stopped.");
        }

        public async Task StopAsync()
        {
            if (this.IsEnabled)
            {
                // avoid new entries to changes queue
                _localDrive.FolderChanged -= _handler;

                // clear changes queue
                _changesQueue.Clear();

                // disable while loop
                this.IsEnabled = false;

                // avoid possible deadlock due to waiting for manual reset event signal
                _manualReset.Set();

                // wait for watch task to finish
                await _watchTask;

                // clear database since from now on we miss events and so we need to re-sync 
                // everything the next time the engine is started
                this.ClearDatabase();
            }

            _logger.LogInformation("Sync engine stopped.");
        }

        // private

        /* high level */
        private async Task WatchForChanges(string folderPath)
        {
            await this.Synchronize(folderPath);
            this.SyncCompleted?.Invoke(this, new SyncCompletedEventArgs(_syncId));
            _syncId++;

            while (this.IsEnabled)
            {
                _manualReset.Wait(_cts.Token);

                if (_cts.IsCancellationRequested)
                    break;

                if (_changesQueue.TryDequeue(out var currentFolderPath))
                {
                    await this.Synchronize(currentFolderPath);
                    this.SyncCompleted?.Invoke(this, new SyncCompletedEventArgs(_syncId));
                    _syncId++;
                }
                else
                {
                    _manualReset.Reset();
                }
            }
        }

        private async Task Synchronize(string folderPath = "")
        {
            _logger.LogInformation($"Synchronizing folder '{folderPath}'.");

            // prepare database
            _dbContext.Database.EnsureCreated();

            // remote drive
            if (_syncMode == SyncMode.TwoWay)
            {
                await _remoteDrive.ProcessDelta(async deltaPage => await this.InternalSynchronize(_remoteDrive, _localDrive, deltaPage),
                                               folderPath, _dbContext, _cts.Token);
            }
            else
            {
                if (!_dbContext.RemoteStates.Any())
                {
                    await _remoteDrive.ProcessDelta(async deltaPage =>
                    {
                        foreach (var driveItem in deltaPage)
                            await this.UpdateDatabase(null, newRemoteState: driveItem.ToRemoteState(), WatcherChangeTypes.Created);
                    }, folderPath, _dbContext, _cts.Token);
                }
            }

            // local drive
            await _localDrive.ProcessDelta(async deltaPage => await this.InternalSynchronize(_localDrive, _remoteDrive, deltaPage),
                                           folderPath, _dbContext, _cts.Token);

            // conflicts
            await this.CheckConflicts();

            // orphaned remote states
            if (_syncMode == SyncMode.Echo)
                await this.DeleteOrphanedRemoteStates();

            _logger.LogInformation("Synchronisation finished.");
        }

        private async Task InternalSynchronize(IDriveProxy sourceDrive, IDriveProxy targetDrive, List<DriveItem> deltaPage)
        {
            var isLocal = sourceDrive == _localDrive;

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
                        this.EnsureLocalConflictByConflictFile(conflictFilePath: newDriveItem.GetPath());
                    }
                    // proceed
                    else
                    {
                        RemoteState oldRemoteState;
                        RemoteState newRemoteState;

                        // find old remote state item
                        if (isLocal)
                            oldRemoteState = _dbContext.RemoteStates.FirstOrDefault(current => current.Path == newDriveItem.GetPath());
                        else
                            oldRemoteState = _dbContext.RemoteStates.FirstOrDefault(current => current.Id == newDriveItem.Id);

                        var oldDriveItem = oldRemoteState?.ToDriveItem();

                        // file is tracked as conflict
                        // action: do nothing, it will be handled by "CheckConflicts" later
                        if (isLocal && _dbContext.Conflicts.Any(conflict => conflict.OriginalFilePath == newDriveItem.GetPath()))
                        {
                            _logger.LogDebug($"File is tracked as conflict. Action(s): do nothing.");
                        }

                        // synchronize
                        (var updatedDriveItem, var changeType) = await this.SyncDriveItem(sourceDrive, targetDrive, oldDriveItem, newDriveItem.MemberwiseClone());

                        // update database
                        if (isLocal)
                        {
                            newRemoteState = updatedDriveItem.ToRemoteState();
                            newRemoteState.IsLocal = true;
                        }
                        else
                        {
                            if (updatedDriveItem.GetPath() == "root")
                                continue;

                            newRemoteState = newDriveItem.ToRemoteState();
                        }

                        await this.UpdateDatabase(oldRemoteState, newRemoteState: newRemoteState, changeType);
                    }
                }
            }
        }

        private async Task<(DriveItem UpdateDriveItem, WatcherChangeTypes ChangeType)> SyncDriveItem(
            IDriveProxy sourceDrive,
            IDriveProxy targetDrive,
            DriveItem oldDriveItem,
            DriveItem newDriveItem)
        {
            DriveItem updatedDriveItem;

            (var itemName1, var itemName2) = this.GetItemNames(newDriveItem);
            var changeType = newDriveItem.GetChangeType(oldDriveItem);

            switch (changeType)
            {
                case WatcherChangeTypes.Changed:
                case WatcherChangeTypes.Created:

                    // new item was created on source drive
                    // actions: create on target drive
                    _logger.LogInformation($"New {itemName2} was created or modified on drive '{sourceDrive.Name}'. Action(s): Create or modify {itemName2} on drive '{targetDrive.Name}'.");
                    updatedDriveItem = await this.TransferDriveItem(sourceDrive, targetDrive, newDriveItem);

                    break;

                case WatcherChangeTypes.Deleted:

                    // item was deleted on source drive
                    // actions: delete item on target drive
                    _logger.LogInformation($"{itemName1} was deleted on drive '{sourceDrive.Name}'. Action(s): Delete {itemName2} on drive '{targetDrive.Name}'.");

                    if (await targetDrive.ExistsAsync(newDriveItem))
                    {
                        updatedDriveItem = await targetDrive.DeleteAsync(newDriveItem);
                    }
                    else
                    {
                        _logger.LogWarning($"Cannot delete {itemName2} because it does not exist on drive '{targetDrive.Name}'.");
                        throw new InvalidOperationException($"Cannot delete {itemName2} because it does not exist on drive '{targetDrive.Name}'.");
                    }

                    break;

                case WatcherChangeTypes.Renamed:

                    // item was renamed / moved on source drive
                    // actions: rename / move item on target drive
                    _logger.LogInformation($"{itemName1} was renamed / moved on drive '{sourceDrive.Name}'. Action(s): Rename / move {itemName2} on drive '{targetDrive.Name}'.");

                    if (await targetDrive.ExistsAsync(newDriveItem))
                    {
                        _logger.LogWarning($"Cannot move {itemName2} because the target {itemName2} already exists on drive '{targetDrive.Name}'.");
                        throw new InvalidOperationException($"Cannot move {itemName2} because the target {itemName2} already exists on drive '{targetDrive.Name}'.");
                    }
                    else
                    {
                        updatedDriveItem = await targetDrive.MoveAsync(oldDriveItem, newDriveItem);
                    }

                    break;

                default:
                    updatedDriveItem = newDriveItem;
                    break;
            }

            return (updatedDriveItem, changeType);
        }

        private void ClearDatabase()
        {
            _dbContext.Database.ExecuteSqlRaw($"DELETE FROM {nameof(RemoteState)}s;");
            _dbContext.Database.ExecuteSqlRaw($"DELETE FROM {nameof(Conflict)}s;");
        }

        private async Task UpdateDatabase(RemoteState oldRemoteState, RemoteState newRemoteState, WatcherChangeTypes changeType)
        {
            switch (changeType)
            {
                case WatcherChangeTypes.Deleted:

                    // remote state was deleted
                    // actions: remove remote state from database
                    _dbContext.RemoteStates.Remove(oldRemoteState);

                    break;

                // remote state was created or modified
                // actions: create or update database state
                case WatcherChangeTypes.Changed:
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Renamed:
                default:

                    if (oldRemoteState == null)
                        _dbContext.RemoteStates.Add(newRemoteState);
                    else
                        _dbContext.Entry(oldRemoteState).CurrentValues.SetValues(newRemoteState);

                    break;
            }

            await _dbContext.SaveChangesAsync();
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
            var originalDriveItem = conflict.OriginalFilePath.ToDriveItem(DriveItemType.File);

            // original file exists locally
            if (await _localDrive.ExistsAsync(originalDriveItem))
            {
                _logger.LogDebug($"Original file exists locally.");
                var conflictDriveItem = conflict.ConflictFilePath.ToDriveItem(DriveItemType.File);

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
                        if (await _localDrive.GetHashAsync(originalDriveItem) == remoteItem.QuickXorHash)
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

        private async Task DeleteOrphanedRemoteStates()
        {
            // files
            foreach (var remoteState in _dbContext.RemoteStates.Where(current => !current.IsLocal && current.Type == DriveItemType.File))
            {
                _logger.LogInformation($"Delete orphaned file '{remoteState.Path}' from drive '{_remoteDrive.Name}'.");
                await _remoteDrive.DeleteAsync(remoteState.ToDriveItem());
            }

            // folders
            foreach (var remoteState in _dbContext.RemoteStates.Where(current => !current.IsLocal && current.Type == DriveItemType.Folder))
            {
                _logger.LogInformation($"Delete orphaned folder '{remoteState.Path}' from drive '{_remoteDrive.Name}'.");
                await _remoteDrive.DeleteAsync(remoteState.ToDriveItem());
            }
        }

        /* low level */
        private async Task<DriveItem> TransferDriveItem(IDriveProxy sourceDrive, IDriveProxy targetDrive, DriveItem driveItem)
        {
            (var itemName1, var itemName2) = this.GetItemNames(driveItem);
            var isLocal = sourceDrive == _localDrive;

            // item exists on target drive
            if (_syncMode == SyncMode.TwoWay && await targetDrive.ExistsAsync(driveItem))
            {
                // item is unchanged on target drive
                // actions: do nothing
                if (await targetDrive.GetLastWriteTimeUtcAsync(driveItem) == driveItem.LastModified())
                {
                    _logger.LogDebug($"{itemName1} already exists and is unchanged on target drive '{targetDrive.Name}'. Action(s): do nothing.");
                }

                // item was modified on target drive
                else
                {
                    if (!isLocal)
                    {
                        _logger.LogDebug($"{itemName1} already exists and was modified on target drive '{targetDrive.Name}'. Action(s): handle conflict.");
                        await this.EnsureLocalConflict(driveItem);
                    }
                }
            }

            // item does not exist on target drive
            // actions: transfer item
            else
            {
                _logger.LogInformation($"{itemName1} is not available on target drive '{targetDrive.Name}'. Action(s): transfer {itemName2}.");
                return await this.InternalTransferDriveItem(sourceDrive, targetDrive, driveItem);
            }

            return driveItem;
        }

        private async Task<DriveItem> InternalTransferDriveItem(IDriveProxy sourceDrive, IDriveProxy targetDrive, DriveItem driveItem)
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

        private async Task EnsureLocalConflict(DriveItem driveItem)
        {
            var conflictDriveItem = driveItem.ToConflict();

            // conflict file does not exist
            // actions: transfer file to local drive
            if (!await _localDrive.ExistsAsync(conflictDriveItem))
            {
                _logger.LogInformation($"Conflict file does not exist on drive '{_localDrive.Name}'. Actions(s): transfer file.");
                await this.InternalTransferDriveItem(_remoteDrive, _localDrive, conflictDriveItem);
            }

            this.EnsureLocalConflictByConflictFile(conflictDriveItem.GetPath());
        }

        private void EnsureLocalConflictByConflictFile(string conflictFilePath)
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

        private (string itemName1, string itemName2) GetItemNames(DriveItem driveItem)
        {
            string itemName1;
            string itemName2;

            switch (driveItem.Type())
            {
                case DriveItemType.Folder:
                    itemName1 = "Folder"; itemName2 = "folder"; break;

                case DriveItemType.File:
                    itemName1 = "File"; itemName2 = "file"; break;

                case DriveItemType.RemoteItem:
                default:
                    throw new ArgumentException();
            }

            return (itemName1, itemName2);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            // cancel all operations
            _cts?.Cancel();

            // avoid possible deadlock due to waiting for manual reset event signal
            _manualReset.Set();

            // wait for watch task to finish
            _watchTask?.Wait();
        }

        #endregion
    }
}
