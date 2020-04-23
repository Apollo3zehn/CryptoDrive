using System.Collections.Generic;

namespace CryptoDrive.Core
{
    public class CryptoDriveContext
    {
        #region Constructors

        public CryptoDriveContext()
        {
            this.RemoteStates = new List<CryptoDriveItem>();
            this.IsInitialized = false;
        }

        #endregion

        #region Properties

        public List<CryptoDriveItem> RemoteStates { get; set; }

        public bool IsInitialized { get; set; }

        #endregion
    }
}
