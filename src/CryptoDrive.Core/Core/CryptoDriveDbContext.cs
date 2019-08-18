using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;

namespace CryptoDrive.Core
{
    public class CryptoDriveDbContext : DbContext
    {
        public CryptoDriveDbContext(DbContextOptions options) : base(options)
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
        [Key]
        public string Id { get; set; }

        public string Path { get; set; }

        public string ETag { get; set; }

        public long Size { get; set; }

        public GraphItemType Type { get; set; }

        public DateTime LastModified { get; set; }
    }

    public class Conflict
    {
        [Key]
        public string ConflictFilePath { get; set; }

        public string OriginalFilePath { get; set; }
    }
}
