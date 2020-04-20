using CryptoDrive.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace CryptoDrive.Core.Tests
{
    public class SynchronizeEchoChangeTrackingTests : IDisposable
    {
        private List<ILoggerProvider> _loggerProviders;
        private ILogger<CryptoDriveSyncEngine> _logger;

        private DriveHive _driveHive;

        private ManualResetEventSlim _manualReset;

        public SynchronizeEchoChangeTrackingTests(ITestOutputHelper xunitLogger)
        {
            _manualReset = new ManualResetEventSlim();
            (_logger, _loggerProviders) = Utils.GetLogger(xunitLogger);
        }

        private async Task Execute(string fileId, Func<Task> actAction, Action assertAction, int syncId)
        {
            Exception ex = null;
            _driveHive = await Utils.PrepareDrives(fileId, _logger);
            var syncEngine = new CryptoDriveSyncEngine(_driveHive.RemoteDrive, _driveHive.LocalDrive, SyncMode.Echo, _logger);

            syncEngine.SyncCompleted += (sender, e) =>
            {
                if (ex == null && e.Exception != null)
                    ex = e.Exception;

                try
                {
                    if (e.SyncId == 0)
                        actAction?.Invoke().Wait();

                    else if (e.SyncId == syncId)
                        syncEngine.Stop();
                }
                finally
                {
                    if (e.SyncId == syncId)
                        _manualReset.Set();
                }
            };

            // Act
            syncEngine.Start();
            _manualReset.Wait(timeout: TimeSpan.FromSeconds(30));

            // Assert
            assertAction?.Invoke();

            if (ex != null)
                throw ex;

            _driveHive.Dispose();
        }

        [Fact]
        public async Task CanSyncSubA_AddTest()
        {
            await this.Execute("/sub/a", async () =>
            {
                /* add new file */
                _logger.LogInformation("TEST: Add file.");
                await _driveHive.LocalDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/b1"]());
            },
            () =>
            {
                Assert.True(File.Exists("/sub/a".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/b".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                Utils.CompareFiles("/sub/a", "/sub/a1", _driveHive.RemoteDrivePath);
            }, syncId: 1);
        }

        [Fact]
        public async Task CanSyncSubA_DeleteTest()
        {
            await this.Execute("/sub/a", async () =>
            {
                /* delete file */
                _logger.LogInformation("TEST: Delete file.");
                await _driveHive.LocalDrive.DeleteAsync(Utils.DriveItemPool["/sub/a1"]());
            },
            () =>
            {
                Assert.True(!File.Exists("/sub/a".ToAbsolutePath(_driveHive.LocalDrivePath)), "File should not exist.");
                Assert.True(!File.Exists("/sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File should not exist.");
            }, syncId: 1);
        }

        [Fact]
        public async Task CanSyncSubA_ModifyTest()
        {
            await this.Execute("/sub/a", async () =>
            {
                var driveItem = Utils.DriveItemPool["/sub/a1"]();

                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("New content.")))
                {
                    driveItem.Content = stream;
                    driveItem.FileSystemInfo.LastModifiedDateTime = DateTime.UtcNow;

                    /* modify file */
                    _logger.LogInformation("TEST: Modify file.");
                    await _driveHive.LocalDrive.CreateOrUpdateAsync(driveItem);
                }
            },
            () =>
            {
                var hashAlgorithm = new QuickXorHash();

                Assert.True(File.Exists("/sub/a".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                using (var stream = File.OpenRead("/sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)))
                {
                    var actual = Convert.ToBase64String(hashAlgorithm.ComputeHash(stream));
                    var expected = Convert.ToBase64String(hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes("New content.")));

                    Assert.True(actual == expected, "The hashes are not equal.");
                }
            }, syncId: 1);
        }

        [Fact]
        public async Task CanSyncSubA_Move1Test()
        {
            await this.Execute("/sub/a", async () =>
            {
                var newDriveItem = Utils.DriveItemPool["/sub/a1"]();
                newDriveItem.ParentReference.Path = newDriveItem.ParentReference.Path.Replace("/sub", "/sub_new");

                /* move file to new folder */
                _logger.LogInformation("TEST: Move file.");
                await _driveHive.LocalDrive.MoveAsync(Utils.DriveItemPool["/sub/a1"](), newDriveItem);
            },
            () =>
            {
                Assert.True(!File.Exists("/sub/a".ToAbsolutePath(_driveHive.LocalDrivePath)), "File should not exist.");
                Assert.True(!File.Exists("/sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File should not exist.");

                Assert.True(File.Exists("/sub_new/a".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/sub_new/a".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");
            }, syncId: 3);
        }

        [Fact]
        public async Task CanSyncSubA_Move2Test()
        {
            await this.Execute("/sub/a", async () =>
            {
                var newDriveItem = Utils.DriveItemPool["/sub/a1"]();
                newDriveItem.Name = newDriveItem.Name.Replace("a", "a_new");

                /* rename file */
                _logger.LogInformation("TEST: Rename file.");
                await _driveHive.LocalDrive.MoveAsync(Utils.DriveItemPool["/sub/a1"](), newDriveItem);
            },
            () =>
            {
                Assert.True(!File.Exists("/sub/a".ToAbsolutePath(_driveHive.LocalDrivePath)), "File should not exist.");
                Assert.True(!File.Exists("/sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File should not exist.");

                Assert.True(File.Exists("/sub/a_new".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/sub/a_new".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");
            }, syncId: 1);
        }

        [Fact]
        public async Task CanSyncSubA_Move3Test()
        {
            await this.Execute("/sub/a", async () =>
            {
                var oldDriveItem = "/sub".ToDriveItem(DriveItemType.Folder);
                var newDriveItem = oldDriveItem.MemberwiseClone();

                newDriveItem.Name = newDriveItem.Name.Replace("sub", "sub_new");

                /* rename folder */
                _logger.LogInformation("TEST: Rename file.");
                await _driveHive.LocalDrive.MoveAsync(oldDriveItem, newDriveItem);
            },
            () =>
            {
                Assert.True(!Directory.Exists("/sub".ToAbsolutePath(_driveHive.LocalDrivePath)), "Folder should not exist.");
                Assert.True(!Directory.Exists("/sub".ToAbsolutePath(_driveHive.RemoteDrivePath)), "Folder should not exist.");

                Assert.True(Directory.Exists("/sub_new".ToAbsolutePath(_driveHive.LocalDrivePath)), "Folder does not exist.");
                Assert.True(Directory.Exists("/sub_new".ToAbsolutePath(_driveHive.RemoteDrivePath)), "Folder does not exist.");
            }, syncId: 1);
        }

        [Fact]
        public async Task CanSyncExtA_Move1Test()
        {
            var externalDrivePath = Path.Combine(Path.GetTempPath(), "CryptoDriveExternal_" + Guid.NewGuid().ToString());
            var externalDrive = new LocalDriveProxy(externalDrivePath, "External", _logger);

            await this.Execute("", async () =>
            {
                await externalDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/sub/a1"]());

                /* move folder from external drive to local drive */
                _logger.LogInformation("TEST: Move folder from external drive to local drive.");
                var newDriveItem = Utils.DriveItemPool["/sub/a1"]();
                var sourcePath = Path.GetDirectoryName(newDriveItem.GetAbsolutePath(externalDrivePath));
                var targetPath = Path.GetDirectoryName(newDriveItem.GetAbsolutePath(_driveHive.LocalDrivePath));

                Directory.Move(sourcePath, targetPath);
            },
            () =>
            {
                Assert.True(Directory.Exists("/sub".ToAbsolutePath(_driveHive.RemoteDrivePath)), "Folder does not exist.");
                Assert.True(File.Exists("/sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");
            }, syncId: 2);

            Directory.Delete(externalDrivePath, true);
        }

        [Fact]
        public async Task CanSyncExtA_Move2Test()
        {
            var externalDrivePath = Path.Combine(Path.GetTempPath(), "CryptoDriveExternal_" + Guid.NewGuid().ToString());
            var externalDrive = new LocalDriveProxy(externalDrivePath, "External", _logger);

            await this.Execute("/sub/a", () =>
            {
                /* move folder from local drive to external drive */
                _logger.LogInformation("TEST: Move folder from local drive to external drive.");
                var newDriveItem = Utils.DriveItemPool["/sub/a1"]();
                var sourcePath = Path.GetDirectoryName(newDriveItem.GetAbsolutePath(_driveHive.LocalDrivePath));
                var targetPath = Path.GetDirectoryName(newDriveItem.GetAbsolutePath(externalDrivePath));

                Directory.Move(sourcePath, targetPath);

                return Task.CompletedTask;
            },
            () =>
            {
                Assert.True(!Directory.Exists("/sub".ToAbsolutePath(_driveHive.RemoteDrivePath)), "Folder should not exist.");
                Assert.True(!File.Exists("/sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File should not exist.");
            }, syncId: 1);

            Directory.Delete(externalDrivePath, true);
        }

        public void Dispose()
        {
            _loggerProviders.ForEach(loggerProvider => loggerProvider.Dispose());

            Directory.Delete(_driveHive.RemoteDrivePath, true);
            Directory.Delete(_driveHive.LocalDrivePath, true);
        }
    }
}