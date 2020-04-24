using CryptoDrive.AccountManagement;
using CryptoDrive.Core;
using CryptoDrive.Cryptography;
using CryptoDrive.Drives;
using Microsoft.Extensions.DependencyInjection;
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
    public partial class AppStateViewModel : BindableBase, IDisposable
    {
        #region Fields

        private object _messageLoglock;

        private string _configFilePath;
        private string _restoreMessage;

        private IServiceProvider _serviceProvider;
        private RestoreFlags _restoreFlags;

        private LoggerSniffer<AppStateViewModel> _logger;
        private List<CryptoDriveSyncEngine> _syncEngines;

        #endregion

        #region Constructors

        public AppStateViewModel(IServiceProvider serviceProvider, ILogger<AppStateViewModel> logger)
        {
            _serviceProvider = serviceProvider;
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
                this.ActiveSyncAccount = this.Config.SyncAccounts.FirstOrDefault();
            }
            else
            {
                this.Config = new CryptoDriveConfiguration();
                Directory.CreateDirectory(Path.GetDirectoryName(_configFilePath));
                this.Config.Save(_configFilePath);
            }

            // key
            if (string.IsNullOrWhiteSpace(this.Config.SymmetricKey))
            {
                this.Config.SymmetricKey = Cryptonizer.GenerateKey(CryptoDriveConfiguration.KeySize);
                this.Config.Save(_configFilePath);
            }

            if (!this.Config.KeyIsSecured)
            {
                this.Config.IsSyncEnabled = false;
                this.ShowKeyDialog = true;
            }

            // restore settings
            this.RestoreSettings = new RestoreSettings()
            {
                RestoreKey = this.Config.SymmetricKey
            };

            this.RestoreMessage = string.Empty;

            // start
            if (this.Config.IsSyncEnabled)
                _ = this.InternalStartAsync(force: true);
        }

        #endregion

        #region Properties

        public string RestoreMessage
        {
            get { return _restoreMessage; }
            set { this.SetProperty(ref _restoreMessage, value); }
        }

        public RestoreFlags RestoreFlags
        {
            get { return _restoreFlags; }
            set { this.SetProperty(ref _restoreFlags, value); }
        }

        public SyncAccount ActiveSyncAccount { get; set; }

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

        public async Task AddSyncAccountAsync(DriveProvider provider)
        {
            this.ShowAddDriveProviderDialog = false;

            var accountManager = this.GetAccountManager(provider);
            var username = await accountManager.SignInAsync();
            var syncAccount = new SyncAccount(provider, username);

            if (!this.Config.SyncAccounts.Any(account => account.Provider == provider && account.Username == username))
            {
                this.Config.SyncAccounts.Add(syncAccount);
                this.SaveConfig();
            }
        }

        public async Task RemoveSyncAccountAsync(SyncAccount syncAccount)
        {
            var accountManager = this.GetAccountManager(syncAccount.Provider);
            await accountManager.SignOutAsync(syncAccount.Username);

            this.Config.SyncAccounts.Remove(syncAccount);
            this.SaveConfig();
        }

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
            this.RaisePropertyChanged(nameof(AppStateViewModel.Config));
        }

        public void AddOrUpdateSyncFolderPair()
        {
            var index = this.ActiveSyncAccount.SyncFolderPairs.IndexOf(this.SelectedSyncFolderPair);

            if (index > -1)
                this.ActiveSyncAccount.SyncFolderPairs[index] = this.SelectedSyncFolderPairEdit;
            else
                this.ActiveSyncAccount.SyncFolderPairs.Add(this.SelectedSyncFolderPair);

            this.Config.Save(_configFilePath);
            this.ShowSyncFolderAddEditDialog = false;
        }

        public void RemoveSyncFolderPair()
        {
            this.ActiveSyncAccount.SyncFolderPairs.Remove(this.SelectedSyncFolderPair);
            this.Config.Save(_configFilePath);
            this.ShowSyncFolderRemoveDialog = false;
        }

        public void ConfirmKeyIsSecured()
        {
            this.Config.KeyIsSecured = true;
            this.Config.Save(_configFilePath);

            this.ShowKeyDialog = false;
        }

        #endregion

        #region Methods

        public void Log(string fileName, string message)
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logFilePath = Path.Combine(localAppDataPath, "CryptoDrive", "Logs", fileName);

            File.AppendAllText(logFilePath, message + Environment.NewLine);
        }

        public async Task<IDriveProxy> GetRemoteDriveProxyAsync(SyncAccount syncAccount)
        {
            IDriveProxy drive;

            switch (syncAccount.Provider)
            {
                case DriveProvider.OneDrive:

                    var oneDriveAccountManager = _serviceProvider.GetRequiredService<IOneDriveService>();
                    var accountType = await oneDriveAccountManager.GetAccountTypeAsync(syncAccount.Username);
                    var graphClient = await oneDriveAccountManager.CreateGraphClientAsync(syncAccount.Username);
                    drive = await OneDriveProxy.CreateAsync(graphClient, accountType, NullLogger.Instance);

                    break;

                case DriveProvider.GoogleDrive:

                    var googleDriveAccountManager = _serviceProvider.GetRequiredService<IGoogleDriveAccountManager>();
                    //var googleDriveClient = await googleDriveAccountManager.CreateGoogleDriveClientAsync();
                    drive = new GoogleDriveProxy();

                    break;

                case DriveProvider.Dropbox:

                    var dropboxAccountManager = _serviceProvider.GetRequiredService<IDropboxAccountManager>();
                    var dropboxClient = await dropboxAccountManager.CreateDropboxClientAsync(syncAccount.Username);
                    drive = new DropboxProxy(dropboxClient);

                    break;

                default:
                    throw new NotSupportedException();
            }

            return drive;
        }

        public void Dispose()
        {
            _logger.OnMessageLogged -= this.OnMessageLogged;
        }

        private async Task InternalStartAsync(bool force = false)
        {
            if (!force && this.Config.IsSyncEnabled)
                throw new Exception("I am already synchronizing.");

            foreach (var syncAccount in this.Config.SyncAccounts)
            {
                foreach (var syncFolderPair in syncAccount.SyncFolderPairs)
                {
                    //var localDrive = new LocalDriveProxy(syncFolderPair.Local,
                    //                                 "Local Drive",
                    //                                 _logger);
                    //var remoteDrive = await OneDriveProxy.CreateAsync(syncFolderPair.Remote,
                    //                                                  _graphService.GraphClient,
                    //                                                  _graphService.GetAccountType(),
                    //                                                  _logger,
                    //                                                  BatchRequestContentPatch.ApplyPatch);

                    //var cryptonizer = new Cryptonizer(this.Config.SymmetricKey);
                    //var syncEngine = new CryptoDriveSyncEngine(remoteDrive, localDrive, cryptonizer, _logger);

                    //_syncEngines.Add(syncEngine);
                    //syncEngine.Start();
                }
            }

            this.Config.IsSyncEnabled = true;
            this.Config.Save(_configFilePath);
        }

        private IAccountManager GetAccountManager(DriveProvider provider)
        {
            return provider switch
            {
                DriveProvider.OneDrive => (IAccountManager)_serviceProvider.GetRequiredService<IOneDriveService>(),
                DriveProvider.GoogleDrive => (IAccountManager)_serviceProvider.GetRequiredService<IGoogleDriveAccountManager>(),
                DriveProvider.Dropbox => (IAccountManager)_serviceProvider.GetRequiredService<IDropboxAccountManager>(),
                _ => throw new NotSupportedException()
            };
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

        #endregion
    }
}
