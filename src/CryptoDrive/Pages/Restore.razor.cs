using CryptoDrive.Core;
using CryptoDrive.Cryptography;
using CryptoDrive.Drives;
using CryptoDrive.Shared;
using CryptoDrive.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDrive.Pages
{
    public partial class Restore
    {
        #region Fields

        private FileExplorer _fileExplorer;
        private CancellationTokenSource _cts;

        #endregion

        #region Constructors

        public Restore()
        {
            this.PropertyChanged = async (sender, e) =>
            {
                if (e.PropertyName == nameof(AppStateViewModel.RestoreMessage))
                    await this.InvokeAsync(this.StateHasChanged);

                else if (e.PropertyName == nameof(AppStateViewModel.RestoreFlags))
                    await this.InvokeAsync(this.StateHasChanged);
            };
        }

        #endregion

        #region Properties

        public string RestoreLogFileName { get; private set; }

        #endregion

        #region Commands

        private async Task RestoreAsync()
        {
            _cts = new CancellationTokenSource();

            var settings = this.AppState.RestoreSettings;
            var drive = _fileExplorer.Drive;
            var sourceFolder = _fileExplorer.NavigationHierarchy.Last();
            var targetFolder = settings.RestoreFolder;
            var cryptonizer = new Cryptonizer(settings.RestoreKey);

            Directory.CreateDirectory(targetFolder);

            this.AppState.ShowRestoreDialog = true;
            this.AppState.RestoreFlags = RestoreFlags.Restoring;
            this.RestoreLogFileName = $"Restore_{DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss")}.txt";

            try
            {
                await this.RestoreHierarchyAsync(sourceFolder, targetFolder, cryptonizer, drive, _cts.Token);
            }
            finally
            {
                this.AppState.RestoreFlags &= ~RestoreFlags.Restoring;
            }
        }

        private void CancelRestore()
        {
            _cts.Cancel();
        }

        #endregion

        #region Methods

        public void OnKeyChanged()
        {

        }

        protected override void OnParametersSet()
        {
            this.AppState.RestoreSettings.SyncAccount = this.AppState.Config.SyncAccounts.FirstOrDefault();
        }

        private async Task RestoreHierarchyAsync(CryptoDriveItem sourceFolder,
                                                 string targetFolder,
                                                 Cryptonizer cryptonizer,
                                                 IDriveProxy drive,
                                                 CancellationToken cancellationToken)
        {
            try
            {
                var driveItems = await drive.GetFolderContentAsync(sourceFolder);

                foreach (var driveItem in driveItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var itemPath = Path.Combine(targetFolder, driveItem.Name);
                    this.AppState.RestoreMessage = driveItem.Name;

                    try
                    {
                        switch (driveItem.Type)
                        {
                            case DriveItemType.File:

                                try
                                {
                                    using (var encryptedStream = await drive.GetFileContentAsync(driveItem))
                                    using (var decryptedStream = cryptonizer.CreateDecryptStream(encryptedStream))
                                    using (var fileStream = File.Create(itemPath))
                                    {
                                        await decryptedStream.CopyToAsync(fileStream, cancellationToken);
                                    }

                                    File.SetLastWriteTimeUtc(itemPath, driveItem.LastModified);
                                }
                                catch
                                {
                                    if (File.Exists(itemPath))
                                        File.Delete(itemPath);

                                    throw;
                                }

                                break;

                            case DriveItemType.Folder:

                                Directory.CreateDirectory(itemPath);
                                await this.RestoreHierarchyAsync(driveItem, itemPath, cryptonizer, drive, cancellationToken);

                                break;

                            default:
                                throw new NotSupportedException();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        //
                    }
                    catch (Exception ex)
                    {
                        this.AppState.Log(this.RestoreLogFileName, $"Could not restore file '{itemPath}'. Reason: {ex.Message}");
                        this.AppState.RestoreFlags |= RestoreFlags.Error;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //
            }
            catch (Exception ex)
            {
                this.AppState.Log(this.RestoreLogFileName, $"Could not restore folder '{sourceFolder.GetAbsolutePath(targetFolder)}'. Reason: {ex.Message}");
                this.AppState.RestoreFlags |= RestoreFlags.Error;
            }
        }

        #endregion
    }
}
