using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CryptoDrive.Core
{
    public interface IDriveProxy
    {
        #region Properties

        string Name { get; }

        #endregion

        #region Change Tracking

        Task ProcessDelta(Func<List<DriveItem>, Task> action);
        Task<(List<DriveItem> DeltaPage, bool IsFirstDelta)> GetDeltaPageAsync();

        #endregion

        #region CRUD

        Task<DriveItem> CreateOrUpdateAsync(DriveItem driveItem);
        Task<DriveItem> MoveAsync(DriveItem oldDriveItem, DriveItem newDriveItem);
        Task<DriveItem> DeleteAsync(DriveItem driveItem);

        #endregion

        #region File Info

        Task<Uri> GetDownloadUriAsync(DriveItem driveItem);
        Task<bool> ExistsAsync(DriveItem driveItem);
        Task<DateTime> GetLastWriteTimeUtcAsync(DriveItem driveItem);
        Task SetLastWriteTimeUtcAsync(DriveItem driveItem);
        Task<string> GetHashAsync(DriveItem driveItem);
        Task<DriveItem> ToFullDriveItem(DriveItem driveItem);

        #endregion
    }
}