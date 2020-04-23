using CryptoDrive.Cryptography;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CryptoDrive.Core.Tests
{
    public class CryptonizerTests
    {
        [Fact]
        public async Task CanEncryptFile()
        {
            // Arrange
            var expected = "Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam.";
            var key = "c0PN6MxITtN/vp1BLLFwGGJJ6idexDAbCVob6llHdo0=";
            var cryptonizer = new Cryptonizer(key);

            using var originalStream = new MemoryStream(Encoding.UTF8.GetBytes(expected));
            using var encryptedStream = new MemoryStream();
            using var decryptedStream = new MemoryStream();

            // Act
            using var encryptStream = cryptonizer.CreateEncryptStream(originalStream);
            await encryptStream.CopyToAsync(encryptedStream);

            // Assert
            encryptedStream.Seek(0, SeekOrigin.Begin);

            using var decryptStream = cryptonizer.CreateDecryptStream(encryptedStream);
            await decryptStream.CopyToAsync(decryptedStream);

            var actual = Encoding.UTF8.GetString(decryptedStream.ToArray());

            Assert.Equal(expected, actual);
        }
    }
}
