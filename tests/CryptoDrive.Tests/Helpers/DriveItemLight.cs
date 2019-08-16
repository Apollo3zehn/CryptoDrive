using Microsoft.Graph;
using System;
using System.IO;
using System.Text;

namespace CryptoDrive.Tests
{
    public class DriveItemLight
    {
        public DriveItemLight()
        {
            //
        }

        public DriveItemLight(DriveItem driveItem)
        {
            this.Name = driveItem.Name;
            this.Content = Convert.ToBase64String(((MemoryStream)driveItem.Content).ToArray());
            this.LastModifiedDateTime = driveItem.LastModifiedDateTime.Value;
        }

        public string Name { get; set; }
        public string Content { get; set; }
        public Stream ContentStream => new MemoryStream(Encoding.Unicode.GetBytes(Content));
        public DateTimeOffset LastModifiedDateTime { get; set; }
    }
}
