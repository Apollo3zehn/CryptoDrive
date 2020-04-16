using System;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDrive.Core.Core
{
    public class Cryptonizer
    {
        #region Fields

        private AesCryptoServiceProvider _cryptoServiceProvider;

        #endregion

        public Cryptonizer(string base64Key)
        {
            _cryptoServiceProvider = new AesCryptoServiceProvider()
            {
                Key = Convert.FromBase64String(base64Key),
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7
            };
        }

        public void Encrypt(Stream inputStream, string targetFilePath)
        {
            var buffer = new byte[1 * 1024 * 1024 * 25];
            var consumedLength = 0L;

            using var encryptedStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write);

            var totalLength = inputStream.Length;
            _cryptoServiceProvider.GenerateIV();
            encryptedStream.Write(_cryptoServiceProvider.IV, 0, _cryptoServiceProvider.IV.Length);

            using var cryptoStream = new CryptoStream(encryptedStream, _cryptoServiceProvider.CreateEncryptor(), CryptoStreamMode.Write);

            while (consumedLength < totalLength)
            {
                var currentLength = (int)Math.Min(buffer.Length, totalLength - consumedLength);
                inputStream.Read(buffer, 0, currentLength);
                cryptoStream.Write(buffer, 0, currentLength);
                consumedLength += currentLength;
            }
        }

        public void Decrypt(Stream inputStream, string targetFilePath)
        {
            var buffer = new byte[1 * 1024 * 1024 * 25];
            var consumedLength = 0L;
            var IvBuffer = new byte[16];

            using var decryptedStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write);

            var totalLength = inputStream.Length - _cryptoServiceProvider.IV.Length;
            inputStream.Read(IvBuffer, 0, _cryptoServiceProvider.IV.Length);
            _cryptoServiceProvider.IV = IvBuffer;

            var cryptoStream = new CryptoStream(inputStream, _cryptoServiceProvider.CreateDecryptor(), CryptoStreamMode.Read);

            while (consumedLength < totalLength)
            {
                var currentLength = (int)Math.Min(buffer.Length, totalLength - consumedLength);
                var decryptedLength = cryptoStream.Read(buffer, 0, currentLength);
                decryptedStream.Write(buffer, 0, decryptedLength);
                consumedLength += currentLength; // important to use currentLength instead of decryptedLength because totalLength is based on that
            }
        }
    }
}
