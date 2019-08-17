using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Graph;

namespace CryptoDrive.Core
{
    public interface IOneDriveClient
    {
        Task<List<DriveItem>> GetDeltaAsync(string token = "");
        Task<string> GetDownloadUrlAsync(string id);
        Task<DriveItem> UploadFileAsync(string localFilePath, string remoteFilePath);
    }
}