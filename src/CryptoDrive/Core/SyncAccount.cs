using CryptoDrive.Drives;
using System.Collections.Generic;

namespace CryptoDrive.Core
{
    public class SyncAccount
    {
        #region Constructors

        private SyncAccount()
        {
            //
        }

        public SyncAccount(DriveProvider provider, string username)
        {
            this.Provider = provider;
            this.Username = username;
            this.SyncFolderPairs = new List<SyncFolderPair>();
        }

        #endregion

        #region Properties

        public DriveProvider Provider { get; set; }

        public string Username { get; set; }

        public List<SyncFolderPair> SyncFolderPairs { get; set; }

        #endregion
    }
}
