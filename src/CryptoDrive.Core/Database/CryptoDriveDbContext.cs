using Microsoft.EntityFrameworkCore;

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
}
