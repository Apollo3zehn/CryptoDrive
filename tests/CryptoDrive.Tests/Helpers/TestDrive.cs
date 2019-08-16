using CryptoDrive.Helpers;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace CryptoDrive.Tests
{
    public class TestDrive
    {
        List<DriveItem> _driveItems;

        public TestDrive()
        {
            _driveItems = new List<DriveItem>();
        }

        public ReadOnlyCollection<DriveItem> DriveItems => new ReadOnlyCollection<DriveItem>(_driveItems);

        public string GetDownloadUrl(string id)
        {
            return id;
        }

        public Stream Download(string id)
        {
            return _driveItems.First(driveItem => driveItem.Id == id).Content;
        }

        public List<DriveItem> GetDelta()
        {
            return _driveItems;
        }

        public DriveItem Upload(DriveItemLight driveItemLight)
        {
            return this.Upload(driveItemLight.Name, driveItemLight.ContentStream, driveItemLight.LastModifiedDateTime);
        }

        public DriveItem Upload(string name, Stream content, DateTimeOffset lastModifiedDateTime)
        {
            var driveItem = new DriveItem()
            {
                Content = content,
                ETag = Convert.ToBase64String(new QuickXorHash().ComputeHash(content)),
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Size = content.Length,
                File = new Microsoft.Graph.File(),
                FileSystemInfo = new Microsoft.Graph.FileSystemInfo() { LastModifiedDateTime = lastModifiedDateTime }
            };

            driveItem.AdditionalData = new Dictionary<string, object>() { { CryptoDriveConstants.DownloadUrl, driveItem.Id } };

            _driveItems.Add(driveItem);

            return driveItem;
        }
    }
}
