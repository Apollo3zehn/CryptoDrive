using System;
using System.Collections.Generic;

namespace CryptoDrive.Core
{
    public class FileSystemChangedEventArgs : EventArgs
    {
        public FileSystemChangedEventArgs(List<string> fileSystemEventArgs)
        {
            this.FileSystemEventArgs = fileSystemEventArgs;
        }

        public List<string> FileSystemEventArgs { get; }
    }
}
