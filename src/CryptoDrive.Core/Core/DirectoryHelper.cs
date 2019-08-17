using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CryptoDrive.Core
{
    public class DirectoryHelper
    {
        public static IEnumerable<string> SafelyEnumerateFolders(string parentFolderPath, string searchPattern, SearchOption searchOption)
        {
            var folders = Enumerable.Empty<string>();

            try
            {
                if (searchOption == SearchOption.AllDirectories)
                    folders = Directory.EnumerateDirectories(parentFolderPath).SelectMany(x => DirectoryHelper.SafelyEnumerateFolders(x, searchPattern, searchOption));

                return folders.Concat(Directory.EnumerateDirectories(parentFolderPath, searchPattern));
            }
            catch (UnauthorizedAccessException)
            {
                return folders;
            }
        }

        public static IEnumerable<string> SafelyEnumerateFiles(string folderPath, string searchPattern, SearchOption searchOption)
        {
            var files = Enumerable.Empty<string>();

            try
            {
                if (searchOption == SearchOption.AllDirectories)
                    files = Directory.EnumerateDirectories(folderPath).SelectMany(x => DirectoryHelper.SafelyEnumerateFiles(x, searchPattern, searchOption));

                return files.Concat(Directory.EnumerateFiles(folderPath, searchPattern));
            }
            catch (UnauthorizedAccessException)
            {
                return files;
            }
        }
    }
}
