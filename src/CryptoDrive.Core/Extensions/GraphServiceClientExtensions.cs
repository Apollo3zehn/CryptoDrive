using Microsoft.Graph;

namespace CryptoDrive.Extensions
{
    public static class GraphServiceClientExtensions
    {
        public static IDriveItemRequestBuilder GetDriveItemRequestBuilder(this IGraphServiceClient graphClient, string itemPath)
        {
            // In principle, ...Root.ItemWithPath("/") should work like 
            // every other path, but with msgraph it doesn't.
            // https://docs.microsoft.com/en-us/onedrive/developer/rest-api/api/driveitem_list_children?view=odsp-graph-online#list-children-of-a-driveitem-with-a-known-path
            if (itemPath == "/")
                return graphClient.Me.Drive.Root;
            else
                return graphClient.Me.Drive.Root.ItemWithPath(itemPath);
        }
    }
}
