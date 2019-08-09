using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace CryptoDrive.Helpers
{
    public static class OneDriveSynchronizer
    {
        private static string _token;

        public static async Task BuildInitialState(GraphServiceClient graphClient, OneDriveContext dbContext)
        {
            //var result = await _client.Subscriptions.Request().AddAsync(new Subscription()
            //{
            //    ChangeType = "updated,deleted",
            //    NotificationUrl = /* skipped */,
            //    ExpirationDateTime = DateTimeOffset.UtcNow.AddMinutes(10),
            //    Resource = "/me/drive/root",
            //}, token);

            var webClient = new WebClient();

            // base directory
            var rootFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CryptoDrive");

            // prepare DB
            dbContext.Database.EnsureCreated();
            dbContext.Database.ExecuteSqlRaw($"DELETE FROM {nameof(RemoteState)};");
            dbContext.Database.ExecuteSqlRaw($"DELETE FROM {nameof(LocalState)};");

            // get delta
            bool isLast = false;
            IDriveItemDeltaCollectionPage delta;

            while (true)
            {
                if (string.IsNullOrWhiteSpace(_token))
                    delta = await graphClient.Me.Drive.Root.Delta().Request().GetAsync();
                else
                    delta = await graphClient.Me.Drive.Root.Delta().Request(new List<Option> { new QueryOption("token", _token) }).GetAsync();

                // extract next token
                if (delta.AdditionalData.ContainsKey("@odata.nextLink"))
                {
                    _token = delta.AdditionalData["@odata.nextLink"].ToString().Split("=")[1];
                }
                else
                {
                    _token = delta.AdditionalData["@odata.deltaLink"].ToString().Split("=")[1];
                    isLast = true;
                }

                // Achtung! Type kann auch "RemoteItem" sein!

                // select remote states and add them to the DB context
                var remoteStates = delta.Select(page => new RemoteState()
                {
                    Id = page.Id,
                    Path = Path.Combine(page.ParentReference.Path.Substring(12), page.Name), // remove "/drive/root:"
                    CTag = page.CTag,
                    ETag = page.ETag,
                    Type = page.Folder != null ? GraphItemType.Folder : GraphItemType.File,
                    LastModified = page.FileSystemInfo.LastModifiedDateTime.Value,
                    DownloadUrl = page.AdditionalData is null ? null : page.AdditionalData["@microsoft.graph.downloadUrl"].ToString()
                }).Where(state => state.Path != "root");

                dbContext.RemoteState.AddRange(remoteStates);

                // exit while loop
                if (isLast)
                    break;
            }

            await dbContext.SaveChangesAsync();

            // loop through local file system
            var options = new EnumerationOptions()
            {
                RecurseSubdirectories = true
            }; 

            foreach (var filePath in Directory.EnumerateFiles(rootFolderPath, "*", options))
            {
                var normalizedPath = filePath.Substring(rootFolderPath.Length + 1).Replace('\\', '/');
                var remoteState = dbContext.RemoteState.FirstOrDefault(current => current.Path == normalizedPath);

                if (remoteState == null)
                {
                    dbContext.LocalState.Add(new LocalState()
                    {
                        Path = normalizedPath
                    });
                }
                else
                {
                    remoteState.IsLocal = true;
                }
            }

            await dbContext.SaveChangesAsync();

            // download missing items
            foreach (var item in dbContext.RemoteState.Where(state => !state.IsLocal))
            {
                var localPath = Path.Combine(rootFolderPath, item.Path);

                switch (item.Type)
                {
                    case GraphItemType.Folder:
                        Directory.CreateDirectory(localPath);
                        break;
                    case GraphItemType.File:
                        webClient.DownloadFile(item.DownloadUrl, localPath);
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            // upload missing items
            foreach (var item in dbContext.LocalState)
            {
                var localPath = Path.Combine(rootFolderPath, item.Path);
                var fileSystemInfo = new FileInfo(localPath);

                var graphFileSystemInfo = new Microsoft.Graph.FileSystemInfo()
                {
                    CreatedDateTime = fileSystemInfo.CreationTimeUtc,
                    LastAccessedDateTime = fileSystemInfo.LastAccessTimeUtc,
                    LastModifiedDateTime = fileSystemInfo.LastWriteTimeUtc
                };

                using (var stream = File.OpenRead(localPath))
                {
                    if (fileSystemInfo.Length <= 4 * 1024 * 1024) // file.Length <= 4 MB
                    {
                        var driveItem = new DriveItem()
                        {
                            File = new Microsoft.Graph.File(),
                            FileSystemInfo = graphFileSystemInfo,
                            Name = Path.GetFileName(item.Path)
                        };

                        try
                        {
                            // https://docs.microsoft.com/en-us/onedrive/developer/rest-api/api/driveitem_post_content?view=odsp-graph-online
                            // https://github.com/microsoftgraph/msgraph-sdk-dotnet/issues/218
                            //await UpDownloadHelper.UploadSmallFile(graphClient, driveItem, stream);

                            var newDriveItem = await graphClient.Me.Drive.Root.ItemWithPath(item.Path).Content.Request().PutAsync<DriveItem>(stream);
                            await graphClient.Me.Drive.Items[newDriveItem.Id].Request().UpdateAsync(driveItem);

                        }
                        catch (Exception ex)
                        {
                            throw;
                        }
                    }
                    else
                    {
                        var properties = new DriveItemUploadableProperties()
                        {
                            FileSystemInfo = graphFileSystemInfo
                        };

                        await UpDownloadHelper.OneDriveUploadLargeFile(graphClient, stream, properties, item.Path);
                    }
                }
            }

            // save database
            await dbContext.SaveChangesAsync();

            
        }
    }
}
