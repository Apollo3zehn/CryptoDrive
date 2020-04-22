using System.Collections.Generic;

namespace CryptoDrive.Core
{
    public class SyncSettings
    {
        #region Constructors

        private SyncSettings()
        {
            //
        }

        public SyncSettings(string provider, string username)
        {
            this.Provider = provider;
            this.Username = username;
            this.SyncFolderPairs = new List<SyncFolderPair>();
        }

        #endregion

        #region Properties

        public string Provider { get; set; }

        public string Username { get; set; }

        public List<SyncFolderPair> SyncFolderPairs { get; set; }

        #endregion
    }
}
