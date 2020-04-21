using CryptoDrive.Core;
using System;
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

        public static string ToAbsolutePath(this string relativePath, string basePath)
        {
            return Path.Combine(basePath, relativePath.TrimStart('/'));
        }

        public static string NormalizeSlashes(this string value)
        {
            value = value.Replace('\\', '/');
            value = value.TrimEnd('/');

            if (!value.StartsWith('/'))
                value = $"/{value}";

            return value;
        }
    }
}
