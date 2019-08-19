using CryptoDrive.Core;
using CryptoDrive.Extensions;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using File = System.IO.File;

namespace CryptoDrive.Tests
{
    public class RemoteTestDrive
    {
        List<DriveItem> _driveItems;
        string _drivePath;

        public RemoteTestDrive(string drivePath)
        {
            _drivePath = drivePath;
            _driveItems = new List<DriveItem>();
        }

        public ReadOnlyCollection<DriveItem> DriveItems => new ReadOnlyCollection<DriveItem>(_driveItems);

        public string GetDownloadUrl(string id)
        {
            return _driveItems.First(driveItem => driveItem.Id == id).AdditionalData[CryptoDriveConstants.DownloadUrl].ToString();
        }

        public List<DriveItem> GetDelta()
        {
            return _driveItems;
        }

        public DriveItem Upload(DriveItem driveItem)
        {
            return this.Upload(driveItem.Name, driveItem.Content, driveItem.FileSystemInfo.LastModifiedDateTime.Value, driveItem.ParentReference);
        }

        public DriveItem Upload(string name, Stream content, DateTimeOffset lastModifiedDateTime, ItemReference parentReference = null)
        {
            if (parentReference == null)
                parentReference = new ItemReference { Path = CryptoDriveConstants.PathPrefix };

            var driveItem = new DriveItem()
            {
                Content = content,
                ETag = Convert.ToBase64String(new QuickXorHash().ComputeHash(content)),
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Size = content.Length,
                File = new Microsoft.Graph.File(),
                FileSystemInfo = new Microsoft.Graph.FileSystemInfo() { LastModifiedDateTime = lastModifiedDateTime },
                ParentReference = parentReference
            };

            var filePath = driveItem.Name.ToAbsolutePath(_drivePath);

            using (var stream = File.OpenWrite(filePath))
            {
                content.Seek(0, SeekOrigin.Begin);
                content.CopyTo(stream);
            }

            driveItem.AdditionalData = new Dictionary<string, object>() 
            { 
                [CryptoDriveConstants.DownloadUrl] = filePath
            };

            _driveItems.Add(driveItem);

            return driveItem;
        }
    }
}
