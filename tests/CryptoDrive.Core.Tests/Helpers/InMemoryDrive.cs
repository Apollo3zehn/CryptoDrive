using CryptoDrive.Core;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace CryptoDrive.Tests
{
    public class InMemoryDrive
    {
        List<DriveItem> _driveItems;

        public InMemoryDrive()
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

        public DriveItem Upload(DriveItem driveItem)
        {
            return this.Upload(driveItem.Name, driveItem.Content, driveItem.LastModifiedDateTime.Value, driveItem.ParentReference);
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

            driveItem.AdditionalData = new Dictionary<string, object>() { { CryptoDriveConstants.DownloadUrl, "https://foo.bar/" + driveItem.Id } };

            _driveItems.Add(driveItem);

            return driveItem;
        }
    }
}
