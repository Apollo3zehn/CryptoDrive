using System.ComponentModel.DataAnnotations;

namespace CryptoDrive.Core
{
    public class RestoreSettings
    {
        #region Properties

        [Required]
        public SyncAccount SyncAccount { get; set; }

        [Required]
        [RestoreKeyValidation]
        public string RestoreKey { get; set; }

        [Required]
        [RestoreFolderValidation]
        public string RestoreFolder { get; set; }

        #endregion
    }
}
