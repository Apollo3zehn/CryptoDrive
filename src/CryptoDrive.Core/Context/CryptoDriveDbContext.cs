using System.Collections.Generic;

namespace CryptoDrive.Core
{
    public class CryptoDriveContext
    {
        public CryptoDriveContext()
        {
            this.RemoteStates = new List<RemoteState>();
            this.Conflicts = new List<Conflict>();
        }

        public List<RemoteState> RemoteStates { get; set; }
        public List<Conflict> Conflicts { get; set; }
    }
}
