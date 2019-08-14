using Microsoft.EntityFrameworkCore;
using System;

namespace CryptoDrive.Helpers
{
    public class OneDriveContext : DbContext
    {
        public OneDriveContext(DbContextOptions options) : base(options)
        {
            //
        }

        public DbSet<RemoteState> RemoteStates { get; set; }
        public DbSet<Conflict> Conflicts { get; set; }
    }

    public enum GraphItemType
    {
        Folder = 0,
        File = 1,
        RemoteItem = 2
    }

    public class RemoteState
    {
        public string Id { get; set; }

        public string Path { get; set; }

        public string ETag { get; set; }

        public long Size { get; set; }

        public GraphItemType Type { get; set; }

        public DateTimeOffset LastModified { get; set; }

        public bool IsLocal { get; set; }
    }

    public class Conflict
    {
        public string FilePath { get; set; }
    }
}
