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

        public static string ToConflictFilePath(this string filePath, DateTimeOffset lastModified)
        {
            var conflictFileName = filePath.ToConflictFileName(lastModified);
            var folderPath = Path.GetDirectoryName(filePath).NormalizeSlashes();

            return PathHelper.Combine(folderPath, conflictFileName);
        }

        public static string ToConflictFileName(this string fileName, DateTimeOffset lastModified)
        {
            var extension = Path.GetExtension(fileName);
            var conflictFileName = $"{fileName} (Conflicted Copy {lastModified.ToString("yyyy-MM-dd HHmmss")})";

            if (!string.IsNullOrWhiteSpace(extension))
                conflictFileName += $".{extension}";

            return conflictFileName;
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
