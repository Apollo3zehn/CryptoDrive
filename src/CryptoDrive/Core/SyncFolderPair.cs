﻿using System.ComponentModel.DataAnnotations;

namespace CryptoDrive.Core
{
    public class SyncFolderPair
    {
        #region Constructors

        public SyncFolderPair()
        {
            //
        }

        #endregion

        #region Properties

        [Required]
        [SyncFolderValidation(CryptoDriveLocation.Local)]
        public string Local { get; set; }

        [Required]
        [SyncFolderValidation(CryptoDriveLocation.Remote)]
        public string Remote { get; set; }

        #endregion
    }
}
