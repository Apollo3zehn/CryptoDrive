using System;
using System.Collections.Generic;

namespace CryptoDrive.Core
{
    public class DriveChangedEventArgs : EventArgs
    {
        public DriveChangedEventArgs(List<string> folderPaths)
        {
            this.FolderPaths = folderPaths;
        }

        public List<string> FolderPaths { get; }
    }
}
