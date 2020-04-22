using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CryptoDrive.Core
{
    public class CryptoDriveConfiguration
    {
        #region Constructors

        public CryptoDriveConfiguration()
        {
            this.LogLevel = LogLevel.Information;
            this.SymmetricKey = string.Empty;
            this.SyncAccounts = new List<SyncSettings>();
        }

        #endregion

        #region Properties

        public static int KeySize { get; } = 256;

        public LogLevel LogLevel { get; set; }

        public bool KeyIsSecured { get; set; }

        public bool IsSyncEnabled { get; set; }

        public string SymmetricKey { get; set; }

        public List<SyncSettings> SyncAccounts { get; set;}

        #endregion

        #region Methods

        public static CryptoDriveConfiguration Load(string filePath)
        {
            var jsonString = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<CryptoDriveConfiguration>(jsonString);
        }

        public void Save(string filePath)
        {
            var jsonString = JsonSerializer.Serialize(this, new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(filePath, jsonString);
        }

        #endregion
    }
}
