using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;

namespace CryptoDrive.Helpers
{
    public class OneDriveContext : DbContext
    {
        public OneDriveContext(DbContextOptions options) : base(options)
        {
            //
        }

        public DbSet<RemoteState> RemoteState { get; set; }
        public DbSet<LocalState> LocalState { get; set; }
    }

    public enum GraphItemType
    {
        Folder = 0,
        File = 1
    }

    public class RemoteState
    {
        public string Id { get; set; }

        public string Path { get; set; }

        public string CTag { get; set; }

        public string ETag { get; set; }

        public GraphItemType Type { get; set; }

        public DateTimeOffset LastModified { get; set; }

        public bool IsLocal { get; set; }

        public string DownloadUrl { get; set; }
    }

    public class LocalState
    {
        [Key]
        public string Path { get; set; }
    }
}
