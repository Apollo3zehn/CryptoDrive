using CryptoDrive.Core;
using Prism.Mvvm;
using System;

namespace CryptoDrive.ViewModels
{
    public partial class AppStateViewModel : BindableBase, IDisposable
    {
        #region Fields

        private bool _showKeyDialog;
        private bool _showAddDriveProviderDialog;
        private bool _showSyncFolderAddEditDialog;
        private bool _showSyncFolderRemoveDialog;
        private bool _showRestoreDialog;

        #endregion

        #region Properties

        public bool ShowKeyDialog
        {
            get { return _showKeyDialog; }
            set { this.SetProperty(ref _showKeyDialog, value); }
        }

        public bool ShowAddDriveProviderDialog
        {
            get { return _showAddDriveProviderDialog; }
            set { this.SetProperty(ref _showAddDriveProviderDialog, value); }
        }

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

        public bool ShowRestoreDialog
        {
            get { return _showRestoreDialog; }
            set { this.SetProperty(ref _showRestoreDialog, value); }
        }

        #endregion

        #region Commands
    
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

        #endregion
    }
}
