﻿using CryptoDrive.Core;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDrive.Drives
{
    public interface IDriveProxy : IDisposable
    {
        #region Events

        public event EventHandler<DriveChangedNotification> FolderChanged;

        #endregion

        #region Properties

        string Name { get; }

        #endregion

        #region Navigation

        Task<List<DriveItem>> GetFolderContentAsync(DriveItem driveItem);

        #endregion

        #region Change Tracking

        Task ProcessDelta(Func<List<DriveItem>, Task> action,
                          string folderPath,
                          CryptoDriveContext context,
                          DriveChangedType changeType,
                          CancellationToken cancellationToken);

        #endregion

        #region CRUD

        Task<DriveItem> CreateOrUpdateAsync(DriveItem driveItem);
        Task<DriveItem> MoveAsync(DriveItem oldDriveItem, DriveItem newDriveItem);
        Task DeleteAsync(DriveItem driveItem);

        #endregion

        #region File Info

        Task<Stream> GetFileContentAsync(DriveItem driveItem);
        Task<bool> ExistsAsync(DriveItem driveItem);

        #endregion
    }
}