using CryptoDrive.Core;
using CryptoDrive.Cryptography;
using CryptoDrive.Drives;
using CryptoDrive.Graph;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoDrive.ViewModels
{
    public class AppStateViewModel : BindableBase, IDisposable
    {
        #region Fields

        private bool _showSyncFolderAddEditDialog;
        private bool _showSyncFolderRemoveDialog;

        private object _messageLoglock;

        private string _username;
        private string _givenName;
        private string _configFilePath;

        private IGraphService _graphService;
        private LoggerSniffer<AppStateViewModel> _logger;

        private List<CryptoDriveSyncEngine> _syncEngines;

        #endregion

        #region Constructors

        public AppStateViewModel(IGraphService graphService, ILogger<AppStateViewModel> logger)
        {
            _logger = new LoggerSniffer<AppStateViewModel>(logger);
            _syncEngines = new List<CryptoDriveSyncEngine>();
            _messageLoglock = new object();

            // logging
            this.MessageLog = new List<string>();
            _logger.OnMessageLogged += this.OnMessageLogged;

            // config
            _configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CryptoDrive", "config.json");

            if (File.Exists(_configFilePath))
            {
                this.Config = CryptoDriveConfiguration.Load(_configFilePath);
            }
            else
            {
                this.Config = new CryptoDriveConfiguration();
                Directory.CreateDirectory(Path.GetDirectoryName(_configFilePath));
                this.Config.Save(_configFilePath);
            }

            // graph service
            _graphService = graphService;

            if (_graphService.IsSignedIn)
                Task.Run(async () => await this.UpdateUserNameAsync()).Wait();

            // find sync settings
            if (this.IsSignedIn)
                this.ActiveSyncSettings = this.AddOrCreateSyncSettings("OneDrive", this.Username);

            // disable sync when conditions are not satisfied
            if (!this.IsSignedIn || !this.Config.KeyIsSecured)
                this.Config.IsSyncEnabled = false;

            // key
            if (string.IsNullOrWhiteSpace(this.Config.SymmetricKey))
            {
                this.Config.SymmetricKey = Cryptonizer.GenerateKey(CryptoDriveConfiguration.KeySize);
                this.Config.Save(_configFilePath);
            }

            if (this.IsSignedIn && !this.Config.KeyIsSecured)
                this.ShowKeyDialog = true;

            // restore settings
            this.RestoreSettings = new RestoreSettings()
            {
                RestoreKey = this.Config.SymmetricKey
            };

            // start
            if (this.Config.IsSyncEnabled)
                _ = this.InternalStartAsync(force: true);
        }

        #endregion

        #region Properties

        public string GivenName
        {
            get { return _givenName; }
            set { this.SetProperty(ref _givenName, value); }
        }

        public string Username
        {
            get { return _username; }
            set { this.SetProperty(ref _username, value); }
        }

        public bool IsSignedIn => _graphService.IsSignedIn;

        public bool ShowKeyDialog { get; set; }

        public bool ShowSyncFolderAddEditDialog
        {
            get { return _showSyncFolderAddEditDialog; }
            set { this.SetProperty(ref _showSyncFolderAddEditDialog, value); }
        }

        public bool ShowSyncFolderRemoveDialog
        {
            get { return _showSyncFolderRemoveDialog; }
            set { this.SetProperty(ref _showSyncFolderRemoveDialog, value); }
        }

        public SyncSettings ActiveSyncSettings { get; private set; }

        public SyncFolderPair SelectedSyncFolderPair { get; private set; }

        public SyncFolderPair SelectedSyncFolderPairEdit { get; private set; }

        public CryptoDriveConfiguration Config { get; }

        public RestoreSettings RestoreSettings { get; set; }

        public List<string> MessageLog { get; }

        #endregion

        #region Relay Properties

        public LogLevel LogLevel
        {
            get 
            { 
                return this.Config.LogLevel;
            }
            set 
            {
                this.Config.LogLevel = value;
                this.Config.Save(_configFilePath);
            }
        }

        #endregion

        #region Commands

        public Task StartAsync()
        {
            return this.InternalStartAsync();
        }

        public async Task StopAsync()
        {
            foreach (var syncEngine in _syncEngines)
            {
                await syncEngine.StopAsync();
            }

            _syncEngines.Clear();

            this.Config.IsSyncEnabled = false;
            this.Config.Save(_configFilePath);
        }

        public void SaveConfig()
        {
            this.Config.Save(_configFilePath);
        }

        public void InitializeAddEditSyncFolderDialog()
        {
            this.SelectedSyncFolderPair = new SyncFolderPair();
            this.SelectedSyncFolderPairEdit = this.SelectedSyncFolderPair;

            this.ShowSyncFolderAddEditDialog = true;
        }

        public void InitializeAddEditSyncFolderDialog(SyncFolderPair syncFolderPair)
        {
            this.SelectedSyncFolderPair = syncFolderPair;
            this.SelectedSyncFolderPairEdit = new SyncFolderPair()
            {
                Local = syncFolderPair.Local,
                Remote = syncFolderPair.Remote
            };

            this.ShowSyncFolderAddEditDialog = true;
        }

        public void InitializeRemoveSyncFolderDialog(SyncFolderPair syncFolderPair)
        {
            this.SelectedSyncFolderPair = syncFolderPair;
            this.ShowSyncFolderRemoveDialog = true;
        }

        public void AddOrUpdateSyncFolderPair()
        {
            var index = this.ActiveSyncSettings.SyncFolderPairs.IndexOf(this.SelectedSyncFolderPair);

            if (index > -1)
                this.ActiveSyncSettings.SyncFolderPairs[index] = this.SelectedSyncFolderPairEdit;
            else
                this.ActiveSyncSettings.SyncFolderPairs.Add(this.SelectedSyncFolderPair);

            this.Config.Save(_configFilePath);
            this.ShowSyncFolderAddEditDialog = false;
        }

        public void RemoveSyncFolderPair()
        {
            this.ActiveSyncSettings.SyncFolderPairs.Remove(this.SelectedSyncFolderPair);
            this.Config.Save(_configFilePath);
            this.ShowSyncFolderRemoveDialog = false;
        }

        public void ConfirmKeyIsSecured()
        {
            this.Config.KeyIsSecured = true;
            this.Config.Save(_configFilePath);

            this.ShowKeyDialog = false;
        }

        public async Task SignInAsync()
        {
            await _graphService.SignInAsync();
            await this.UpdateUserNameAsync();

            this.ActiveSyncSettings = this.AddOrCreateSyncSettings("OneDrive", this.Username);
            
            this.RaisePropertyChanged(nameof(AppStateViewModel.IsSignedIn));
        }

        public async Task SignOutAsync()
        {
            await _graphService.SignOutAsync();
            await this.UpdateUserNameAsync();

            this.ActiveSyncSettings = null;

            this.RaisePropertyChanged(nameof(AppStateViewModel.IsSignedIn));
        }

        #endregion

        #region Methods

        public async Task<IDriveProxy> GetRemoteDriveProxyAsync()
        {
            return await OneDriveProxy.CreateAsync(_graphService.GraphClient, NullLogger.Instance);
        }

        public void Dispose()
        {
            _logger.OnMessageLogged -= this.OnMessageLogged;
        }

        private async Task UpdateUserNameAsync()
        {
            if (this.IsSignedIn)
            {
                var user = await _graphService.GraphClient.Me.Request().GetAsync();
                this.GivenName = user.GivenName;
                this.Username = user.UserPrincipalName;
            }
            else
            {
                this.GivenName = null;
                this.Username = null;
            }
        }

        private async Task InternalStartAsync(bool force = false)
        {
            if (!force && this.Config.IsSyncEnabled)
                throw new Exception("I am already synchronizing.");

            foreach (var syncFolderPair in this.ActiveSyncSettings.SyncFolderPairs)
            {
                var localDrive = new LocalDriveProxy(syncFolderPair.Local, "Local Drive", _logger);
                var remoteDrive = await OneDriveProxy.CreateAsync(syncFolderPair.Remote, _graphService.GraphClient, _logger, BatchRequestContentPatch.ApplyPatch);

                var cryptonizer = new Cryptonizer(this.Config.SymmetricKey);
                var syncEngine = new CryptoDriveSyncEngine(remoteDrive, localDrive, cryptonizer, _logger);

                _syncEngines.Add(syncEngine);
                syncEngine.Start();
            }

            this.Config.IsSyncEnabled = true;
            this.Config.Save(_configFilePath);
        }

        private void OnMessageLogged(object sender, LogMessageEventArgs e)
        {
            if (e.LogLevel >= this.LogLevel)
            {
                lock (_messageLoglock)
                {
                    this.MessageLog.Add(e.Message);

                    if (this.MessageLog.Count > 10)
                        this.MessageLog.RemoveAt(0);

                    this.RaisePropertyChanged(nameof(this.MessageLog));
                }
            }
        }

        private SyncSettings AddOrCreateSyncSettings(string provider, string username)
        {
            var syncSettings = this.Config.SyncAccounts
                .FirstOrDefault(account => account.Provider == provider && account.Username == username);

            if (syncSettings == null)
            {
                syncSettings = new SyncSettings(provider, username);

                this.Config.SyncAccounts.Add(syncSettings);
                this.Config.Save(_configFilePath);
            }

            return syncSettings;
        }

        #endregion
    }
}
