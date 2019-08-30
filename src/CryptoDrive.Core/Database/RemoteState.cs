using System;
using System.ComponentModel.DataAnnotations;

namespace CryptoDrive.Core
{
    public class RemoteState
    {
        [Key]
        public string Id { get; set; }
        public string Path { get; set; }
        public string QuickXorHash { get; set; }
        public long Size { get; set; }
        public DriveItemType Type { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsLocal { get; set; }
    }
}
