using System.ComponentModel.DataAnnotations;

namespace CryptoDrive.Core
{
    public class Conflict
    {
        [Key]
        public string ConflictFilePath { get; set; }

        public string OriginalFilePath { get; set; }
    }
}
