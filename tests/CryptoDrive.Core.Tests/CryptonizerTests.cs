using CryptoDrive.Core.Core;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using Xunit;

namespace CryptoDrive.Core.Tests
{
    public class CryptonizerTests
    {
        [Fact]
        public void CanEncryptFile()
        {
            // Arrange
            var key = "c0PN6MxITtN/vp1BLLFwGGJJ6idexDAbCVob6llHdo0=";
            var cryptonizer = new Cryptonizer(key);
            var originalFilePath = Path.Combine(Path.GetTempPath(), "original_file.txt");
            var encryptedFilePath = Path.Combine(Path.GetTempPath(), "encrypted_file.txt");
            var decryptedFilePath = Path.Combine(Path.GetTempPath(), "decrypted_file.txt");
            //var expected = "Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam.";
            var expected = "00000000010000000001000000000100000000010000001";

            File.WriteAllText(originalFilePath, expected);

            // Act
            using (var fileStream = File.Open(originalFilePath, FileMode.Open, FileAccess.Read))
            {
                cryptonizer.Encrypt(fileStream, encryptedFilePath);
            }

            // Assert
            using (var fileStream = File.Open(encryptedFilePath, FileMode.Open, FileAccess.Read))
            {
                cryptonizer.Decrypt(fileStream, decryptedFilePath);
            }

            var actual = File.ReadAllText(decryptedFilePath);

            Assert.Equal(expected, actual);
        }
    }
}
