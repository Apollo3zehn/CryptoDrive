using System.Collections.Generic;
using System.Linq;

namespace CryptoDrive.Core
{
    internal static class CoreUtilities
    {
        public static string PathCombine(string basePath, string itemName)
        {
            if (basePath == "/")
                return $"{basePath}{itemName}";
            else
                return $"{basePath}/{itemName}";
        }

        public static List<DriveChangedNotification> MergeChanges(Dictionary<string, DriveChangedType> changesToTypeMap)
        {
            var changeNotifications = new List<DriveChangedNotification>();

            // This class uses a dictionary to collect changes for a specific path with the dictionary
            // key being the path itself. Thus there can be no two entries with the same path.
            foreach (var entry in changesToTypeMap)
            {
                var newFolderPath = entry.Key;
                var changeType = entry.Value;

                // 1. Ensure that there is no descendant change notification already covering parts of the current change notification scope.
                if (changeType == DriveChangedType.Descendants)
                {
                    var descendantsToRemove = changeNotifications
                        .Where(changeNotification => CoreUtilities.IsAncestorOf(newFolderPath, changeNotification.FolderPath)).ToList();

                    foreach (var descendant in descendantsToRemove)
                    {
                        changeNotifications.Remove(descendant);
                    }
                }

                // 2. Ensure that there is no ancestor change notification already covering the scope of the current change notification.
                var ancestors = changeNotifications
                    .Where(changeNotification => CoreUtilities.IsAncestorOf(changeNotification.FolderPath, newFolderPath)).ToList();

                if (!ancestors.Any(ancestor => ancestor.ChangeType == DriveChangedType.Descendants))
                    changeNotifications.Add(new DriveChangedNotification(newFolderPath, changeType));
            }

            return changeNotifications;
        }

        public static bool IsAncestorOf(string folderPath, string testPath)
        {
            var folderPathParts = folderPath.Split('/');
            var testPathParts = testPath.Split('/');

            return folderPathParts.Length < testPathParts.Length &&                     // "/documents/abc" is not parent of "/documents"
                   testPath.StartsWith(folderPath) &&                                   // "/documents/abc" is not parent of "/yyy/documents/abc/def"
                   folderPathParts.Last() == testPathParts[folderPathParts.Length - 1]; // "/documents" is not parent of "/documents - Copy"
        }
    }
}
