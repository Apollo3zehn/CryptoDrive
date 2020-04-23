using CryptoDrive.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;

namespace CryptoDrive.Core
{
    public class ThrottleFileWatcher : IDisposable
    {
        #region Events

        public event EventHandler<DriveChangedEventArgs> DriveChanged;

        #endregion

        #region Fields

        private string _basePath;
        private object _changesLock;

        private Timer _timer;
        private TimeSpan _throttleInterval;

        private FileSystemWatcher _fileWatcher;
        private FileSystemWatcher _folderWatcher;
        private Dictionary<string, DriveChangedType> _driveChanges;

        #endregion

        #region Constructors

        public ThrottleFileWatcher(string basePath, TimeSpan throttleInterval = default)
        {
            _basePath = basePath;
            _changesLock = new object();
            _driveChanges = new Dictionary<string, DriveChangedType>();

            if (throttleInterval == TimeSpan.Zero)
                throttleInterval = TimeSpan.FromSeconds(5);

            _throttleInterval = throttleInterval;

            _timer = new Timer()
            {
                AutoReset = true,
                Enabled = true,
                Interval = _throttleInterval.TotalMilliseconds
            };

            _timer.Elapsed += (sender, e) => this.OnTimerElapsed();

            // file watcher
            _fileWatcher = new FileSystemWatcher()
            {
                IncludeSubdirectories = true,
                InternalBufferSize = 64000,
                NotifyFilter = NotifyFilters.FileName 
                             | NotifyFilters.LastWrite 
                             | NotifyFilters.Size,
                Path = basePath,
            };

            _fileWatcher.Changed += this.OnItemChanged;
            _fileWatcher.Created += this.OnItemChanged;
            _fileWatcher.Deleted += this.OnItemChanged;
            _fileWatcher.Renamed += this.OnItemChanged;

            // folder watcher
            _folderWatcher = new FileSystemWatcher()
            {
                IncludeSubdirectories = true,
                InternalBufferSize = 64000,
                NotifyFilter = NotifyFilters.DirectoryName,
                Path = basePath,
            };

            _folderWatcher.Changed += this.OnItemChanged;
            _folderWatcher.Created += this.OnItemChanged;
            _folderWatcher.Deleted += this.OnItemChanged;
            _folderWatcher.Renamed += this.OnItemChanged;
        }

        #endregion

        #region Properties

        public bool EnableRaisingEvents
        {
            get 
            {
                return _fileWatcher.EnableRaisingEvents; 
            }
            set 
            {
                _fileWatcher.EnableRaisingEvents = value;
                _folderWatcher.EnableRaisingEvents = value;
            }
        }

        #endregion

        #region Methods

        private void OnItemChanged(object source, FileSystemEventArgs e)
        {
            var isFolder = source == _folderWatcher;
            var relativePath = e.FullPath.Substring(_basePath.Length + 1).NormalizeSlashes();
            var parentPath = Path.GetDirectoryName(relativePath).NormalizeSlashes();

            // Folder.
            if (isFolder)
            {
                switch (e.ChangeType)
                {
                    case WatcherChangeTypes.Changed:
                        throw new NotSupportedException("A changed event cannot be processed.");

                    case WatcherChangeTypes.Created:
                    case WatcherChangeTypes.Deleted:
                        this.AddChange(parentPath, DriveChangedType.Self);
                        this.AddChange(relativePath, DriveChangedType.Descendants);
                        break;

                    // "WatcherChangeTypes.Renamed" will only be raised when source 
                    // and target folder paths are pointing to the same parent folder.
                    case WatcherChangeTypes.Renamed:
                        var renamedEventArgs = (RenamedEventArgs)e;
                        var oldRelativePath = renamedEventArgs.OldFullPath.Substring(_basePath.Length + 1).NormalizeSlashes();
                        this.AddChange(parentPath, DriveChangedType.Self);
                        this.AddChange(relativePath, DriveChangedType.Descendants);
                        this.AddChange(oldRelativePath, DriveChangedType.Descendants);
                        break;

                    default:
                        // do nothing
                        break;
                }
            }
            // File. (The file watcher also captures 
            // folder "changed" events (NotifyFilters.LastWrite).)
            else if (e.ChangeType != WatcherChangeTypes.Changed || !Directory.Exists(e.FullPath))
            {
                switch (e.ChangeType)
                {
                    case WatcherChangeTypes.Changed:
                    case WatcherChangeTypes.Created:
                    case WatcherChangeTypes.Deleted:
                        this.AddChange(parentPath, DriveChangedType.Self);
                        break;

                    // "WatcherChangeTypes.Renamed" will only be raised when source 
                    // and target file paths are pointing to the same parent folder.
                    case WatcherChangeTypes.Renamed:
                        this.AddChange(parentPath, DriveChangedType.Self);
                        break;

                    default:
                        // do nothing
                        break;
                }
            }

            // reset timer
            _timer.Interval = _throttleInterval.TotalMilliseconds;
        }

        private void AddChange(string folderPath, DriveChangedType changeType)
        {
            lock (_changesLock)
            {
                // add new change with full scope, overwrite any existing entries
                if (changeType == DriveChangedType.Descendants)
                    _driveChanges[folderPath] = DriveChangedType.Descendants;

                // add new change with light scope only if key does not already exists
                else if (!_driveChanges.ContainsKey(folderPath))
                    _driveChanges[folderPath] = DriveChangedType.Self;
            }
        }

        private void OnTimerElapsed()
        {
            lock (_changesLock)
            {
                if (_driveChanges.Any())
                {
                    var changeNotifications = CoreUtilities.MergeChanges(_driveChanges);
                    this.DriveChanged?.Invoke(this, new DriveChangedEventArgs(changeNotifications));
                }

                _driveChanges.Clear();
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
