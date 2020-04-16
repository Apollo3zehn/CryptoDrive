﻿using CryptoDrive.Core;
using CryptoDrive.Graph;
using Microsoft.Extensions.Logging;
using Prism.Mvvm;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CryptoDrive.ViewModels
{
    public class AppStateViewModel : BindableBase, IDisposable
    {
        #region Fields

        private object _messageLoglock;

        private string _configFilePath;
        private string _userName;

        private SyncFolderPair _selectedSyncFolderPair;
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

            // start
            if (this.Config.IsSyncEnabled)
                this.InternalStartSync(force: true);
        }

        #endregion

        #region Properties

        public bool ShowSyncFolderDeleteDialog { get; set; }

        public List<string> MessageLog { get; }

        public CryptoConfiguration Config { get; }

        public bool IsSignedIn => _graphService.IsSignedIn;

        public string UserName
        {
            get { return _userName; }
            set { this.SetProperty(ref _userName, value); }
        }

        #endregion

        #region Commands

        public void StartSync()
        {
            this.InternalStartSync();
        }

        public async Task StopSyncAsync()
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

        public void AddSyncFolderPair()
        {
            this.Config.SyncFolderPairs.Add(new SyncFolderPair());
            this.SaveConfig();
        }

        public void RemoveSyncFolderPair()
        {
            this.Config.SyncFolderPairs.Remove(_selectedSyncFolderPair);
            this.SaveConfig();
            this.ShowSyncFolderDeleteDialog = false;
        }

        public void InitializeRemoveSyncFolderDialog(SyncFolderPair syncFolderPair)
        {
            _selectedSyncFolderPair = syncFolderPair;
            this.ShowSyncFolderDeleteDialog = true;
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

        #endregion

        #region Methods

        public void Dispose()
        {
            _logger.OnMessageLogged -= this.OnMessageLogged;
        }

        private async Task UpdateUserNameAsync()
        {
            var user = await _graphService.GraphClient.Me.Request().GetAsync();
            this.UserName = user.GivenName;
        }

        private void InternalStartSync(bool force = false)
        {
            if (!force && this.Config.IsSyncEnabled)
            {
                throw new Exception("I am already synchronizing.");
            }

            foreach (var syncFolderPair in this.Config.SyncFolderPairs)
            {
                var localDrive = new LocalDriveProxy(syncFolderPair.Local, "Local Drive", _logger);
                var remoteDrive = new OneDriveProxy(_graphService.GraphClient, _logger, BatchRequestContentPatch.ApplyPatch);
                var syncEngine = new CryptoDriveSyncEngine(remoteDrive, localDrive, SyncMode.Echo, _logger);

                _syncEngines.Add(syncEngine);
                syncEngine.Start(syncFolderPair.Remote);
            }

            this.Config.IsSyncEnabled = true;
            this.Config.Save(_configFilePath);
        }

        private void OnMessageLogged(object sender, string e)
        {
            lock (_messageLoglock)
            {
                this.MessageLog.Add(e);

                if (this.MessageLog.Count > 10)
                    this.MessageLog.RemoveAt(0);

                this.RaisePropertyChanged(nameof(this.MessageLog));
            }
        }

        #endregion
    }
}
