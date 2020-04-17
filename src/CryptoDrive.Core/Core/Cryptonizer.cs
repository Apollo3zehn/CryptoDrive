using System;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDrive.Core
{
    public class Cryptonizer
    {
        #region Fields

        private object _lock;
        private AesCryptoServiceProvider _cryptoServiceProvider;

        #endregion

        public static string GenerateKey()
        {
            return Convert.ToBase64String(new AesCryptoServiceProvider().Key);
        }

        public Cryptonizer(string base64Key)
        {
            _lock = new object();

            // default key size is 256
            _cryptoServiceProvider = new AesCryptoServiceProvider()
            {
                Key = Convert.FromBase64String(base64Key),
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7
            };
        }

        public CryptoIVStream CreateEncryptStream(Stream sourceStream)
        {
            lock (_lock)
            {
                _cryptoServiceProvider.GenerateIV(); 
                var cryptoStream = new CryptoStream(sourceStream, _cryptoServiceProvider.CreateEncryptor(), CryptoStreamMode.Read);
                return new CryptoIVStream(cryptoStream, _cryptoServiceProvider.IV);
            }
        }

        public CryptoStream CreateDecryptStream(Stream sourceStream)
        {
            lock (_lock)
            {
                var ivBuffer = new byte[16];

                sourceStream.Read(ivBuffer);
                _cryptoServiceProvider.IV = ivBuffer;

                return new CryptoStream(sourceStream, _cryptoServiceProvider.CreateDecryptor(), CryptoStreamMode.Read);
            }
        }
    }
}
