using System;

namespace CryptoDrive.Core
{
    public class RemoteState
    {
        #region Properties

        public string Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public DriveItemType Type { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsLocal { get; set; }

        #endregion

        #region Methods

        public string GetItemPath()
        {
            return Utilities.PathCombine(this.Path, this.Name);
        }

        #endregion
    }
}
