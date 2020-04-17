using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CryptoDrive.Core
{
    public class CryptoConfiguration
    {
        #region Constructors

        public CryptoConfiguration()
        {
            this.SymmetricKey = string.Empty;
            this.SyncFolderPairs = new List<SyncFolderPair>();
        }

        #endregion

        #region Properties

        public LogLevel LogLevel { get; set; }

        public bool KeyIsSecured { get; set; }

        public bool IsSyncEnabled { get; set; }

        public string SymmetricKey { get; set; }

        public List<SyncFolderPair> SyncFolderPairs { get; set; }

        #endregion

        #region Methods

        public static CryptoConfiguration Load(string filePath)
        {
            var jsonString = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<CryptoConfiguration>(jsonString);
        }

        public void Save(string filePath)
        {
            var jsonString = JsonSerializer.Serialize(this, new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(filePath, jsonString);
        }

        #endregion
    }
}
