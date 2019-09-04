using System.Collections.Generic;

namespace CryptoDrive.Core
{
    public class CryptoDriveContext
    {
        public CryptoDriveContext()
        {
            this.RemoteStates = new List<RemoteState>();
            this.Conflicts = new List<Conflict>();
            this.IsInitialized = false;
        }

        public List<RemoteState> RemoteStates { get; set; }
        public List<Conflict> Conflicts { get; set; }
        public bool IsInitialized { get; set; }
    }
}
