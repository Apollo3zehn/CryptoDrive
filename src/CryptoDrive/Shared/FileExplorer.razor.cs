using CryptoDrive.Extensions;
using CryptoDrive.Graph;
using HarmonyLib;
using Microsoft.AspNetCore.Components;
using Microsoft.Graph;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoDrive.Shared
{
    public partial class FileExplorer
    {
        #region Fields

        private IGraphServiceClient _graphClient;
        private List<string> _navigationHierarchy;

        #endregion

        #region Constructors

        public FileExplorer()
        {
            _navigationHierarchy = new List<string>();
            this.FolderContent = new List<DriveItem>();
        }

        #endregion

        #region Properties

        [Inject]
        public IGraphService GraphService { get; set; }

        public IReadOnlyList<string> NavigationHierarchy => _navigationHierarchy;

        public List<DriveItem> FolderContent { get; set; }

        #endregion

        #region Commands

        public async Task NavigateDownAsync(string folderName)
        {
            var folderPath = this.GetFolderPath(_navigationHierarchy.AddItem(folderName));

            this.FolderContent = (await _graphClient.GetDriveItemRequestBuilder(folderPath).Children
               .Request()
               .GetAsync())
               .ToList();

            _navigationHierarchy.Add(folderName);
        }

        public async Task NavigateToAsync(int index)
        {
            _navigationHierarchy = _navigationHierarchy.Take(index + 1).ToList();
            var folderPath = this.GetFolderPath(_navigationHierarchy);

            this.FolderContent = (await _graphClient.GetDriveItemRequestBuilder(folderPath).Children
               .Request()
               .GetAsync())
               .ToList();
        }

        #endregion

        #region Methods

        protected override async Task OnParametersSetAsync()
        {
            _graphClient = this.GraphService.GraphClient;
            await this.NavigateDownAsync("/");
        }

        private string GetFolderPath(IEnumerable<string> navigationHierarchy)
        {
            return navigationHierarchy
                .Join(delimiter: "/")
                .Replace("//", "/");
        }

        #endregion
    }
}
