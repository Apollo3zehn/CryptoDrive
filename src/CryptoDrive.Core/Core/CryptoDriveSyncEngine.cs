using CryptoDrive.Extensions;
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

        private CryptoDriveContext _context;
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
                                     SyncMode syncMode,
                                     ILogger logger)
        {
            _remoteDrive = remoteDrive;
            _localDrive = localDrive;
            _syncMode = syncMode;
            _logger = logger;

            _context = new CryptoDriveContext();
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

        public void Start(string folderPath = "/")
        {
            // if already running, throw exception
            if (this.IsEnabled)
                throw new InvalidOperationException("The sync engine is already started.");

            // sync engine and change tracking is now enabled
            this.IsEnabled = true;

            // create watch task
            _watchTask = Task.Run(async () => await this.WatchForChanges(folderPath));

            // add folder change event handler to local drive
            _localDrive.FolderChanged += _handler;

            _logger.LogDebug("Sync engine started.");
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
                this.ClearContext();
            }

            _logger.LogDebug("Sync engine stopped.");
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
                this.ClearContext();
            }

            _logger.LogDebug("Sync engine stopped.");
        }

        // private

        /* high level */
        private async Task WatchForChanges(string folderPath)
        {
            await this.TrySynchronize(folderPath, SyncScope.Full);

            while (this.IsEnabled)
            {
                _manualReset.Wait(_cts.Token);

                if (_cts.IsCancellationRequested)
                    break;

                if (_changesQueue.TryDequeue(out var currentFolderPath))
                    await this.TrySynchronize(currentFolderPath, SyncScope.Light);
                else
                    _manualReset.Reset();
            }
        }

        private async Task TrySynchronize(string folderPath, SyncScope syncScope)
        {
            try
            {
                await this.Synchronize(folderPath, syncScope);
                this.SyncCompleted?.Invoke(this, new SyncCompletedEventArgs(_syncId, null));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Sync failed with message '{ex.Message}'.");
                this.SyncCompleted?.Invoke(this, new SyncCompletedEventArgs(_syncId, ex));
            }
            finally
            {
                _syncId++;
            }
        }

        private async Task Synchronize(string folderPath, SyncScope syncScope)
        {
            _logger.LogInformation($"Syncing folder '{folderPath}'.");

            // remote drive
            if (_syncMode == SyncMode.TwoWay)
            {
#warning Currently not working, and maybe never will. Two-way asynchronous sync is extremly complicated.
                _logger.LogInformation($"Search for changes on remote drive '{_remoteDrive.Name}'.");

                await _remoteDrive.ProcessDelta(async deltaPage => await this.InternalSynchronize(_remoteDrive, _localDrive, deltaPage),
                                                folderPath, _context, syncScope, _cts.Token);
            }
            else
            {
                if (!_context.IsInitialized)
                {
                    _logger.LogInformation($"Building item index of remote drive '{_remoteDrive.Name}'.");

                    await _remoteDrive.ProcessDelta(deltaPage =>
                    {
                        foreach (var driveItem in deltaPage)
                        {
                            if (!string.IsNullOrWhiteSpace(driveItem.Name)) // MS account "apollo3zehndev" gives a nameless drive-item (deleted folder)
                                this.UpdateContext(null, newRemoteState: driveItem.ToRemoteState(), WatcherChangeTypes.Created);
                        }

                        return Task.CompletedTask;
                    }, folderPath, _context, SyncScope.Full, _cts.Token);

                    _context.IsInitialized = true;
                }
            }

            // local drive
            _logger.LogInformation($"Search for changes on local drive '{_localDrive.Name}'.");
            await _localDrive.ProcessDelta(async deltaPage => await this.InternalSynchronize(_localDrive, _remoteDrive, deltaPage),
                                           folderPath, _context, syncScope, _cts.Token);

            //// conflicts
            //await this.CheckConflicts();

            // orphaned remote states
            //if (_syncMode == SyncMode.Echo)
            //    await this.DeleteOrphanedRemoteStates();

            _logger.LogDebug("Sync finished.");
        }

        private async Task InternalSynchronize(
            IDriveProxy sourceDrive,
            IDriveProxy targetDrive,
            List<DriveItem> deltaPage)
        {
            var isLocal = sourceDrive == _localDrive;

            foreach (var newDriveItem in deltaPage)
            {
                using (_logger.BeginScope(new Dictionary<string, object>
                {
                    ["ItemPath"] = newDriveItem.GetItemPath()
                }))
                {
                    _logger.LogInformation($"Syncing item '{newDriveItem.GetItemPath()}'.");

                    try
                    {
                        // if file is marked as conflicted copy
                        if (isLocal && _regex_conflict.IsMatch(newDriveItem.Name))
                        {
                            this.EnsureLocalConflictByConflictFile(conflictFilePath: newDriveItem.GetItemPath());
                        }
                        // proceed
                        else
                        {
                            RemoteState oldRemoteState;
                            RemoteState newRemoteState;

                            // find old remote state item
                            if (isLocal)
                                oldRemoteState = _context.RemoteStates.FirstOrDefault(current => current.GetItemPath() == newDriveItem.GetItemPath());
                            else
                                oldRemoteState = _context.RemoteStates.FirstOrDefault(current => current.Id == newDriveItem.Id);

                            var oldDriveItem = oldRemoteState?.ToDriveItem();

#warning Check this.
                            // This prevents that a file rename can be tracked but it is unclear how to determine a local ID instead?
                            // Maybe the change tracking algorithm can find the ID of a renamed file using the context's remote state list?
                            if (isLocal && oldDriveItem != null)
                                newDriveItem.Id = oldDriveItem.Id;

                            // file is tracked as conflict
                            // action: do nothing, it will be handled by "CheckConflicts" later
                            if (isLocal && _context.Conflicts.Any(conflict => conflict.OriginalFilePath == newDriveItem.GetItemPath()))
                            {
                                _logger.LogDebug($"File is tracked as conflict. Action(s): do nothing.");
                            }

                            // synchronize
                            (var updatedDriveItem, var changeType) = await this.SyncDriveItem(sourceDrive, targetDrive, oldDriveItem, newDriveItem.MemberwiseClone());

                            // update database
                            if (changeType == WatcherChangeTypes.Deleted)
                            {
                                this.UpdateContext(oldRemoteState, null, changeType);
                            }
                            else
                            {
                                if (isLocal)
                                {
                                    newRemoteState = updatedDriveItem.ToRemoteState();
                                    newRemoteState.IsLocal = true;
                                }
                                else
                                {
                                    if (updatedDriveItem.GetItemPath() == "root")
                                        continue;

                                    newRemoteState = newDriveItem.ToRemoteState();
                                }

                                this.UpdateContext(oldRemoteState, newRemoteState, changeType);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Syncing item '{newDriveItem.GetItemPath()}' failed with message '{ex.Message}'.");
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
            DriveItem updatedDriveItem = null;

            (var itemName1, var itemName2) = this.GetItemNames(newDriveItem);
            var changeType = newDriveItem.GetChangeType(oldDriveItem);

            switch (changeType)
            {
                case WatcherChangeTypes.Changed:
                case WatcherChangeTypes.Created:

                    // Item was created or modified on source drive
                    // actions: create or modify on target drive
                    _logger.LogDebug($"{itemName1} was created or modified on drive '{sourceDrive.Name}'. Action(s): Create or modify {itemName2} on drive '{targetDrive.Name}'.");
                    updatedDriveItem = await this.TransferDriveItem(sourceDrive, targetDrive, newDriveItem);

                    break;

                case WatcherChangeTypes.Deleted:

                    // item was deleted on source drive
                    // actions: delete item on target drive
                    _logger.LogDebug($"{itemName1} was deleted on drive '{sourceDrive.Name}'. Action(s): Delete {itemName2} on drive '{targetDrive.Name}'.");

                    if (await targetDrive.ExistsAsync(newDriveItem))
                    {
                        await targetDrive.DeleteAsync(newDriveItem);
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
                    _logger.LogDebug($"{itemName1} was renamed / moved on drive '{sourceDrive.Name}'. Action(s): Rename / move {itemName2} on drive '{targetDrive.Name}'.");

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

        private void ClearContext()
        {
            _context.RemoteStates.Clear();
            _context.Conflicts.Clear();
        }

        private void UpdateContext(RemoteState oldRemoteState, RemoteState newRemoteState, WatcherChangeTypes changeType)
        {
            switch (changeType)
            {
                case WatcherChangeTypes.Deleted:

                    // remote state was deleted
                    // actions: remove remote state from database
                    _context.RemoteStates.Remove(oldRemoteState);

                    break;

                // remote state was created or modified
                // actions: create or update database state
                case WatcherChangeTypes.Changed:
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Renamed:

                    if (oldRemoteState != null)
                        _context.RemoteStates.Remove(oldRemoteState);

                    _context.RemoteStates.Add(newRemoteState);

                    break;

                // nothing changed
                default:
                    break;
            }
        }

        private async Task CheckConflicts()
        {
            var resolvedConflicts = new List<Conflict>();

            foreach (var conflict in _context.Conflicts)
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

            foreach (var resolvedConflict in resolvedConflicts)
            {
                _context.Conflicts.Remove(resolvedConflict);
            }
        }

        private async Task<bool> CheckConflict(Conflict conflict)
        {
            var remoteItem = _context.RemoteStates.FirstOrDefault(current => current.GetItemPath() == conflict.OriginalFilePath);
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
                            _logger.LogDebug($"File was modified. Action(s): upload and replace file.");

                            originalDriveItem = await _localDrive.ToFullDriveItem(originalDriveItem);
                            var driveItem = await _remoteDrive.CreateOrUpdateAsync(originalDriveItem);
                            _context.RemoteStates.Add(driveItem.ToRemoteState());
                        }
                    }

                    // remote file is not tracked in database (e.g. upload failed previously)
                    // actions: upload file
                    else
                    {
                        _logger.LogDebug($"Remote file is not tracked in database. Action(s): upload file.");

                        originalDriveItem = await _localDrive.ToFullDriveItem(originalDriveItem);
                        var driveItem = await _remoteDrive.CreateOrUpdateAsync(originalDriveItem);
                        _context.RemoteStates.Add(driveItem.ToRemoteState());
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
            foreach (var remoteState in _context.RemoteStates.Where(current => !current.IsLocal && current.Type == DriveItemType.File))
            {
                _logger.LogDebug($"Delete orphaned file '{remoteState.GetItemPath()}' from drive '{_remoteDrive.Name}'.");
                await _remoteDrive.DeleteAsync(remoteState.ToDriveItem());
            }

            // folders
            foreach (var remoteState in _context.RemoteStates.Where(current => !current.IsLocal && current.Type == DriveItemType.Folder))
            {
                _logger.LogDebug($"Delete orphaned folder '{remoteState.GetItemPath()}' from drive '{_remoteDrive.Name}'.");
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
                _logger.LogDebug($"{itemName1} is not available on target drive '{targetDrive.Name}'. Action(s): Transfer {itemName2}.");
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
#warning catch more specific error message
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
                _logger.LogDebug($"Conflict file does not exist on drive '{_localDrive.Name}'. Actions(s): Transfer file.");
                await this.InternalTransferDriveItem(_remoteDrive, _localDrive, conflictDriveItem);
            }

            this.EnsureLocalConflictByConflictFile(conflictDriveItem.GetItemPath());
        }

        private void EnsureLocalConflictByConflictFile(string conflictFilePath)
        {
            var conflict = _context.Conflicts.FirstOrDefault(current => current.ConflictFilePath == conflictFilePath);

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

                _context.Conflicts.Add(conflict);
            }
            else
            {
                _logger.LogDebug($"Conflict entity already exists. Actions(s): do nothing.");
            }
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
