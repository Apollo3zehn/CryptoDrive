using CryptoDrive.Core;
using CryptoDrive.Graph;
using Microsoft.Extensions.Logging;
using Prism.Mvvm;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CryptoDrive.ViewModels
{
    public class AppStateViewModel : BindableBase
    {
        #region Fields

        private string _configFilePath;
        private string _userName;
        private IGraphService _graphService;
        private ILogger<AppStateViewModel> _logger;

        #endregion

        #region Constructors

        public AppStateViewModel(IGraphService graphService, ILogger<AppStateViewModel> logger)
        {
            _logger = logger;

            // graph service
            _graphService = graphService;

            if (_graphService.IsSignedIn)
                _ = this.UpdateUserNameAsync();

            // config
            _configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CryptoDrive", "config.json");

            if (File.Exists(_configFilePath))
            {
                this.Config = CryptoConfiguration.Load(_configFilePath);
            }
            else
            {
                this.Config = new CryptoConfiguration();
                Directory.CreateDirectory(Path.GetDirectoryName(_configFilePath));
                this.Config.Save(_configFilePath);
            }
        }

        #endregion

        #region Properties

        public CryptoConfiguration Config { get; }

        public bool IsSignedIn => _graphService.IsSignedIn;

        public string UserName
        {
            get { return _userName; }
            set { this.SetProperty(ref _userName, value); }
        }

        #endregion

        #region Methods

        public void Synchronize()
        {
            foreach (var syncFolderPair in this.Config.SyncFolderPairs)
            {
                var localDrive = new LocalDriveProxy(syncFolderPair.Local, "Local Drive", _logger);
                var remoteDrive = new OneDriveProxy(_graphService.GraphClient, _logger, BatchRequestContentPatch.ApplyPatch);
                var syncEngine = new CryptoDriveSyncEngine(remoteDrive, localDrive, SyncMode.Echo, _logger);

                syncEngine.Start(syncFolderPair.Remote);
            }
        }

        public void SaveConfig()
        {
            this.Config.Save(_configFilePath);
        }

        public void AddSyncFolderPair()
        {
            this.Config.SyncFolderPairs.Add(new SyncFolderPair());
            this.SaveConfig();
        }

        public void RemoveSyncFolderPair(SyncFolderPair syncFolderPair)
        {
            this.Config.SyncFolderPairs.Remove(syncFolderPair);
            this.SaveConfig();
        }

        public async Task SignInAsync()
        {
            await _graphService.SignInAsync();
            await this.UpdateUserNameAsync();

            this.RaisePropertyChanged(nameof(AppStateViewModel.IsSignedIn));
        }

        public async Task SignOutAsync()
        {
            await _graphService.SignOutAsync();
            this.RaisePropertyChanged(nameof(AppStateViewModel.IsSignedIn));
        }

        private async Task UpdateUserNameAsync()
        {
            var user = await _graphService.GraphClient.Me.Request().GetAsync();
            this.UserName = user.GivenName;
        }

        #endregion
    }
}
