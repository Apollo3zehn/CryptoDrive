using CryptoDrive.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Timers;

namespace CryptoDrive.Core
{
    public class PollingWatcher : IDisposable
    {
        #region Events

        public event EventHandler<DriveChangedEventArgs> DriveChanged;

        #endregion

        #region Fields

        private FileSystemWatcher _fileWatcher;
        private Dictionary<string, SyncScope> _changesToScopeMap;
        private object _changesLock;
        private Timer _timer;
        private TimeSpan _pollInterval;

        #endregion

        #region Constructors

        public PollingWatcher(string basePath, TimeSpan pollInterval = default)
        {
            _changesLock = new object();
            _changesToScopeMap = new Dictionary<string, SyncScope>();

            if (pollInterval == TimeSpan.MinValue)
                pollInterval = TimeSpan.FromSeconds(5);

            _pollInterval = pollInterval;

            _timer = new Timer()
            {
                AutoReset = true,
                Enabled = true,
                Interval = _pollInterval.TotalMilliseconds
            };

            _timer.Elapsed += (sender, e) => this.OnTimerElapsed();

            _fileWatcher = new FileSystemWatcher()
            {
                IncludeSubdirectories = true,
                InternalBufferSize = 64000,
                NotifyFilter = NotifyFilters.DirectoryName 
                             | NotifyFilters.FileName 
                             | NotifyFilters.LastWrite 
                             | NotifyFilters.Size,
                Path = basePath,
            };

            _fileWatcher.Changed += this.OnItemChanged;
            _fileWatcher.Created += this.OnItemChanged;
            _fileWatcher.Deleted += this.OnItemChanged;
            _fileWatcher.Renamed += this.OnItemChanged;           
        }

        #endregion

        #region Properties

        public bool EnableRaisingEvents
        {
            get { return _fileWatcher.EnableRaisingEvents; }
            set { _fileWatcher.EnableRaisingEvents = value; }
        }

        #endregion

        #region Methods

        private void OnItemChanged(object source, FileSystemEventArgs e)
        {
            var syncScope = SyncScope.Light;

            lock (_changesLock)
            {
                if (Directory.Exists(e.FullPath))
                {
                    switch (e.ChangeType)
                    {
                        // Whenever the content of a directory changes, there will be two events:
                        //
                        // 1. Exact change (create file, delete folder, ...)
                        //
                        // 2. Change event for parent folder, which is useless here since we are not 
                        //    syncing the 'last modified date' of folders
                        //
                        // => skip these events
                        case WatcherChangeTypes.Changed:
                            return;

                        // If a folder was simply created manually, then a light scope would be sufficient.
                        // However, if the folder was moved from another location it could contain files,
                        // thus full sync scope is required.
                        case WatcherChangeTypes.Created:
                            syncScope = SyncScope.Full;
                            break;

                        default:
                            // do nothing
                            break;
                    }
                }

                var relativePath = e.FullPath.Substring(_fileWatcher.Path.Length + 1);
                var folderPath = Path.GetDirectoryName(relativePath).NormalizeSlashes();

                // add new change with full scope, overwrite any existing entries
                if (syncScope == SyncScope.Full)
                    _changesToScopeMap[folderPath] = SyncScope.Full;

                // add new change with light scope only if key does not already exists
                else if (!_changesToScopeMap.ContainsKey(folderPath))
                    _changesToScopeMap[folderPath] = SyncScope.Light;

                // reset timer
                _timer.Interval = _pollInterval.TotalMilliseconds;
            }
        }

        private void OnTimerElapsed()
        {
            lock (_changesLock)
            {
                if (_changesToScopeMap.Any())
                {
                    var driveChangedNotifications = _changesToScopeMap
                        .Select(entry => new DriveChangedNotification(entry.Key, entry.Value))
                        .ToList();

                    this.DriveChanged?.Invoke(this, new DriveChangedEventArgs(driveChangedNotifications));
                }

                _changesToScopeMap.Clear();
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Dispose();
        }

        #endregion
    }
}
