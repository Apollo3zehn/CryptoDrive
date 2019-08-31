using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;

namespace CryptoDrive.Core
{
    public class PollingWatcher : IDisposable
    {
        #region Events

        public event EventHandler<FileSystemChangedEventArgs> FileSystemChanged;

        #endregion

        #region Fields

        private FileSystemWatcher _fileWatcher;
        private HashSet<string> _changesHashSet;
        private object _changesLock;
        private Timer _timer;
        private TimeSpan _pollInterval;

        #endregion

        #region Constructors

        public PollingWatcher(string basePath, TimeSpan pollInterval = default)
        {
            _changesLock = new object();
            _changesHashSet = new HashSet<string>();

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
            lock (_changesLock)
            {
                var folderPath = Path.GetDirectoryName(e.FullPath.Substring(_fileWatcher.Path.Length + 1));
                _changesHashSet.Add(folderPath);

                // reset timer
                _timer.Interval = _pollInterval.TotalMilliseconds;
            }
        }

        private void OnTimerElapsed()
        {
            lock (_changesLock)
            {
                if (_changesHashSet.Any())
                    this.FileSystemChanged?.Invoke(this, new FileSystemChangedEventArgs(_changesHashSet.ToList()));

                _changesHashSet.Clear();
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
