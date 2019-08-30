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

        public static string ToConflictFilePath(this string filePath, DateTimeOffset lastModified)
        {
            var path = Path.GetDirectoryName(filePath);
            var name = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);
            var conflictedFilePath = $"{Path.Combine(path, name)} (Conflicted Copy {lastModified.ToString("yyyy-MM-dd HHmmss")})";

            if (!string.IsNullOrWhiteSpace(extension))
                conflictedFilePath += $".{extension}";

            return conflictedFilePath;
        }

        public static string ToAbsolutePath(this string relativePath, string basePath)
        {
            return Path.Combine(basePath, relativePath.TrimStart('/')).NormalizeSlashes();
        }

        public static string NormalizeSlashes(this string value)
        {
            return value.Replace('\\', '/').TrimEnd('/');
        }
    }
}
