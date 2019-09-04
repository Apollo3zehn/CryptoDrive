namespace CryptoDrive.Core
{
    public static class PathHelper
    {
        public static string Combine(string basePath, string itemName)
        {
            if (basePath == "/")
                return $"{basePath}{itemName}";
            else
                return $"{basePath}/{itemName}";
        }
    }
}
