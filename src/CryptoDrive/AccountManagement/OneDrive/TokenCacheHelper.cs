using Microsoft.Identity.Client;
using System;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDrive.AccountManagement
{
    public static class TokenCacheHelper
    {
        #region Fields

        private static string _cacheFilePath;
        private static object _fileLock;

        #endregion

        #region Constructors

        static TokenCacheHelper()
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _cacheFilePath = Path.Combine(localAppDataPath, "CryptoDrive", "Drives", "OneDrive", "msalcache.bin3");
            _fileLock = new object();
        }

        #endregion

        #region Methods

        public static void EnableSerialization(ITokenCache tokenCache)
        {
            tokenCache.SetBeforeAccess(TokenCacheHelper.BeforeAccessNotification);
            tokenCache.SetAfterAccess(TokenCacheHelper.AfterAccessNotification);
        }

        private static void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            lock (_fileLock)
            {
                byte[] unprotectedData = null;

                if (File.Exists(_cacheFilePath))
                {
                    var rawData = File.ReadAllBytes(_cacheFilePath);
                    unprotectedData = ProtectedData.Unprotect(rawData, null, DataProtectionScope.CurrentUser);
                }

                args.TokenCache.DeserializeMsalV3(unprotectedData);
            }
        }

        private static void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            if (args.HasStateChanged)
            {
                lock (_fileLock)
                {
                    var rawData = args.TokenCache.SerializeMsalV3();
                    var protectedData = ProtectedData.Protect(rawData, null, DataProtectionScope.CurrentUser);

                    Directory.CreateDirectory(Path.GetDirectoryName(_cacheFilePath));
                    File.WriteAllBytes(_cacheFilePath, protectedData);
                }
            }
        }

        #endregion
    }
}
