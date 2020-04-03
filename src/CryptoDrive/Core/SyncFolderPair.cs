using System.Collections.Generic;

namespace CryptoDrive.Core
{
    public class SyncFolderPair
    {
        #region Constructors

        //public SyncFolderPair(string local, string remote)
        //{
        //    this.Local = local;
        //    this.Remote = remote;
        //}

        public SyncFolderPair()
        {
            //
        }

        #endregion

        #region Properties

        public string Local { get; set; }

        public string Remote { get; set; }

        #endregion
    }
}
