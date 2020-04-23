using CryptoDrive.Cryptography;
using CryptoDrive.Drives;
using CryptoDrive.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        private IDriveProxy _remoteDrive;
        private IDriveProxy _localDrive;
        private ILogger _logger;


        private Task _watchTask;
        private Cryptonizer _cryptonizer;
        private CryptoDriveContext _context;
        private CancellationTokenSource _cts;
        private ManualResetEventSlim _manualReset;

        private EventHandler<DriveChangedNotification> _handler;
        private ConcurrentQueue<DriveChangedNotification> _changesQueue;

        private long _syncId;

        #endregion

        #region Constructors

        public CryptoDriveSyncEngine(IDriveProxy remoteDrive,
                                     IDriveProxy localDrive,
                                     ILogger logger)
            : this(remoteDrive, localDrive, null, logger)
        {
           //
        }

        public CryptoDriveSyncEngine(IDriveProxy remoteDrive,
                                     IDriveProxy localDrive,
                                     Cryptonizer cryptonizer,
                                     ILogger logger)
        {
            _remoteDrive = remoteDrive;
            _localDrive = localDrive;
            _cryptonizer = cryptonizer;
            _logger = logger;

            _context = new CryptoDriveContext();
            _syncId = 0;

            // changes
            _manualReset = new ManualResetEventSlim();
            _changesQueue = new ConcurrentQueue<DriveChangedNotification>();

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

            // create new cancellation token source
            _cts = new CancellationTokenSource();

            // sync engine and change tracking are now enabled
            this.IsEnabled = true;

            // create watch task
            _watchTask = Task.Run(async () => await this.WatchForChanges(folderPath));

            // add folder change event handler to local drive
            _localDrive.FolderChanged += _handler;

            _logger.LogDebug("Sync engine started.");
        }

        public async Task StopAsync()
        {
            if (this.IsEnabled)
            {
                // avoid new entries to changes queue
                _localDrive.FolderChanged -= _handler;

                // clear changes queue
                _changesQueue.Clear();

                // signal cancellation token
                _cts.Cancel();

                // wait for watch task to finish
                try
                {
                    await _watchTask;
                }
                catch (OperationCanceledException)
                {
                    //
                }

                // clear database since from now on we miss events and so we need to re-sync 
                // everything the next time the engine is started
                this.ClearContext();

                //
                this.IsEnabled = false;
            }

            _logger.LogDebug("Sync engine stopped.");
        }

        // private

        /* high level */
        private async Task WatchForChanges(string folderPath)
        {
            await this.TrySynchronize(folderPath, DriveChangedType.Descendants);

            while (!_cts.IsCancellationRequested)
            {
                _manualReset.Wait(_cts.Token);

                if (_changesQueue.TryDequeue(out var driveChangedNotification))
                    await this.TrySynchronize(driveChangedNotification.FolderPath, driveChangedNotification.ChangeType);
                else
                    _manualReset.Reset();
            }
        }

        private async Task TrySynchronize(string folderPath, DriveChangedType changeType)
        {
            try
            {
                await this.Synchronize(folderPath, changeType);
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

        private async Task Synchronize(string folderPath, DriveChangedType changeType)
        {
            _logger.LogInformation($"Syncing folder '{folderPath}'.");

            // remote drive
            if (!_context.IsInitialized)
            {
                _logger.LogInformation($"Building item index of remote drive '{_remoteDrive.Name}'.");

                await _remoteDrive.ProcessDelta(deltaPage =>
                {
                    foreach (var driveItem in deltaPage)
                    {
                        this.UpdateContext(null, newRemoteState: driveItem, WatcherChangeTypes.Created);
                    }

                    return Task.CompletedTask;
                }, folderPath, _context, DriveChangedType.Descendants, _cts.Token);

                _context.IsInitialized = true;
            }

            // local drive
            _logger.LogInformation($"Search for changes on local drive '{_localDrive.Name}'.");
            await _localDrive.ProcessDelta(async deltaPage => await this.InternalSynchronize(_localDrive, _remoteDrive, deltaPage),
                                           folderPath, _context, changeType, _cts.Token);

            _logger.LogDebug("Sync finished.");
        }

        private async Task InternalSynchronize(
            IDriveProxy sourceDrive,
            IDriveProxy targetDrive,
            List<CryptoDriveItem> deltaPage)
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
                        CryptoDriveItem oldRemoteState;
                        CryptoDriveItem newRemoteState;

                        _cts.Token.ThrowIfCancellationRequested();

                        // find old remote state item
                        if (isLocal)
                            oldRemoteState = _context.RemoteStates.FirstOrDefault(current => current.GetItemPath() == newDriveItem.GetItemPath());
                        else
                            oldRemoteState = _context.RemoteStates.FirstOrDefault(current => current.Id == newDriveItem.Id);

                        var oldDriveItem = oldRemoteState;

#warning Check this.
                        // This prevents that a file rename can be tracked but it is unclear how to determine a local ID instead?
                        // Maybe the change tracking algorithm can find the ID of a renamed file using the context's remote state list?
                        if (isLocal && oldDriveItem != null)
                            newDriveItem.Id = oldDriveItem.Id;

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
                                newRemoteState = updatedDriveItem;
                            else
                                newRemoteState = newDriveItem;

                            this.UpdateContext(oldRemoteState, newRemoteState, changeType);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // do nothing
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Syncing item '{newDriveItem.GetItemPath()}' failed with message '{ex.Message}'.");
                    }
                }
            }
        }

        private async Task<(CryptoDriveItem UpdateDriveItem, WatcherChangeTypes ChangeType)> SyncDriveItem(
            IDriveProxy sourceDrive,
            IDriveProxy targetDrive,
            CryptoDriveItem oldDriveItem,
            CryptoDriveItem newDriveItem)
        {
            CryptoDriveItem updatedDriveItem = null;

            (var itemName1, var itemName2) = this.GetItemNames(newDriveItem);

            // With encryption enabled, the encrypted and decrypted file sizes are not equal anymore. 
            // The encrypted file can be calculated, but is not unique. So, currently, rely only
            // on modified date.
            var compareSize = _cryptonizer == null;
            var changeType = newDriveItem.GetChangeType(oldDriveItem, compareSize: compareSize);

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
        }

        private void UpdateContext(CryptoDriveItem oldRemoteState, CryptoDriveItem newRemoteState, WatcherChangeTypes changeType)
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

        /* low level */
        private async Task<CryptoDriveItem> TransferDriveItem(IDriveProxy sourceDrive, IDriveProxy targetDrive, CryptoDriveItem driveItem)
        {
            (var itemName1, var itemName2) = this.GetItemNames(driveItem);

            // item does not exist on target drive
            // actions: transfer item
            _logger.LogDebug($"{itemName1} is not available on target drive '{targetDrive.Name}'. Action(s): Transfer {itemName2}.");
            return await this.InternalTransferDriveItem(sourceDrive, targetDrive, driveItem);
        }

        private async Task<CryptoDriveItem> InternalTransferDriveItem(IDriveProxy sourceDrive, IDriveProxy targetDrive, CryptoDriveItem driveItem)
        {
            switch (driveItem.Type)
            {
                case DriveItemType.Folder:
                    return await targetDrive.CreateOrUpdateAsync(driveItem, null, _cts.Token);

                case DriveItemType.File:

                    var isLocal = sourceDrive == _localDrive;

                    using (var originalStream = await sourceDrive.GetFileContentAsync(driveItem))
                    using (var stream = this.GetTransformedStream(originalStream, isLocal))
                    {
                        return await targetDrive.CreateOrUpdateAsync(driveItem, stream, _cts.Token);
                    }

                default:
                    throw new NotSupportedException();
            }
        }

        private Stream GetTransformedStream(Stream stream, bool isLocal)
        {
            if (_cryptonizer != null)
            {
                if (isLocal)
                    stream = _cryptonizer.CreateEncryptStream(stream);
                else
                    stream = _cryptonizer.CreateDecryptStream(stream);
            }

            return stream;
        }

        private (string itemName1, string itemName2) GetItemNames(CryptoDriveItem driveItem)
        {
            string itemName1;
            string itemName2;

            switch (driveItem.Type)
            {
                case DriveItemType.Folder:
                    itemName1 = "Folder"; itemName2 = "folder"; break;

                case DriveItemType.File:
                    itemName1 = "File"; itemName2 = "file"; break;

                default:
                    throw new NotSupportedException();
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
