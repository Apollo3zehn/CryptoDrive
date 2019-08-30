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

            var folderPath = Path.GetDirectoryName(filePath);
            var conflictedFilePath = Path.Combine(folderPath, conflictFileName);

            return conflictedFilePath.NormalizeSlashes();
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
            return Path.Combine(basePath, relativePath.TrimStart('/')).NormalizeSlashes();
        }

        public static string NormalizeSlashes(this string value)
        {
            return value.Replace('\\', '/').TrimEnd('/');
        }
    }
}
