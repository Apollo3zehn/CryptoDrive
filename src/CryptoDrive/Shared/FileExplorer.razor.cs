using CryptoDrive.Core;
using CryptoDrive.Drives;
using CryptoDrive.Extensions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoDrive.Shared
{
    public partial class FileExplorer
    {
        #region Fields

        private List<CryptoDriveItem> _navigationHierarchy;

        #endregion

        #region Constructors

        public FileExplorer()
        {
            _navigationHierarchy = new List<CryptoDriveItem>();
            this.FolderContent = new List<CryptoDriveItem>();
        }

        #endregion

        #region Properties

        public IDriveProxy Drive { get; private set; }

        public IReadOnlyList<CryptoDriveItem> NavigationHierarchy => _navigationHierarchy;

        public List<CryptoDriveItem> FolderContent { get; set; }

        #endregion

        #region Commands

        public async Task NavigateDownAsync(CryptoDriveItem folder)
        {
            this.FolderContent = await this.Drive.GetFolderContentAsync(folder);
            _navigationHierarchy.Add(folder);
        }

        public async Task NavigateToAsync(int index)
        {
            _navigationHierarchy = _navigationHierarchy.Take(index + 1).ToList();
            var folder = _navigationHierarchy.Last();

            this.FolderContent = await this.Drive.GetFolderContentAsync(folder);
        }

        #endregion

        #region Methods

        public string GetFileIcon(CryptoDriveItem driveItem)
        {
            var extension = Path.GetExtension(driveItem.Name);

            return extension switch
            {
                ".docx" => "file-word",
                ".xlsx" => "file-excel",
                ".pptx" => "file-powerpoint",
                ".pdf"  => "file-pdf",
                ".jpg"  => "file-image",
                ".jpeg" => "file-image",
                ".png"  => "file-image",
                ".tiff" => "file-image",
                _       => "file"
            };
        }

        protected override async Task OnParametersSetAsync()
        {
            this.Drive = await this.AppState.GetRemoteDriveProxyAsync(DriveProvider.OneDrive);
            var driveItem = "/".ToDriveItem(DriveItemType.Folder);

            await this.NavigateDownAsync(driveItem);
        }

        #endregion
    }
}
