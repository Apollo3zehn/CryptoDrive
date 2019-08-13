using System;
using System.IO;

namespace CryptoDrive.Extensions
{
    public static class StreamExtensions
    {
        public static string ConvertToBase64(this Stream stream)
        {
            string base64;

            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                base64 = Convert.ToBase64String(memoryStream.ToArray());
            }

            return base64;
        }
    }
}
