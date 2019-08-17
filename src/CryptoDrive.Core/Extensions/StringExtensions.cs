using System.IO;
using System.Text;

namespace CryptoDrive.Extensions
{
    public static class StringExtensions
    {
        public static MemoryStream ToMemorySteam(this string value)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(value));
        }
    }
}
