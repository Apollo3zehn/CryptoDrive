using CryptoDrive.Core;
using Microsoft.Graph;

namespace CryptoDrive.Drives
{
    public static class DriveItemExtensions
    {
        public static CryptoDriveItem ToCryptoDriveItem(this DriveItem driveItem, string basePrefix)
        {
            if (driveItem.ParentReference.Path.Length == basePrefix.Length)
                driveItem.ParentReference.Path = "/";
            else
                driveItem.ParentReference.Path = driveItem.ParentReference.Path.Substring(basePrefix.Length);

            var id = driveItem.Id;
            var name = driveItem.Name;
            var path = driveItem.ParentReference.Path;
            var type = driveItem.Folder != null ? DriveItemType.Folder : DriveItemType.File;

            return new CryptoDriveItem(id, name, path, type)
            {
                IsDeleted = driveItem.Deleted != null,
                LastModified = driveItem.FileSystemInfo.LastModifiedDateTime.Value.UtcDateTime,
                Size = type == DriveItemType.File ? driveItem.Size.Value : 0,
            };
        }
    }
}
