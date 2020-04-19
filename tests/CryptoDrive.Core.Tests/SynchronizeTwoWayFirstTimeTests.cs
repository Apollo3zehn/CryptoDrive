using CryptoDrive.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace CryptoDrive.Core.Tests
{
    public class SynchronizeTwoWayFirstTimeTests : IDisposable
    {
        private List<ILoggerProvider> _loggerProviders;
        private ILogger<CryptoDriveSyncEngine> _logger;

        private DriveHive _driveHive;

        public SynchronizeTwoWayFirstTimeTests(ITestOutputHelper xunitLogger)
        {
            (_logger, _loggerProviders) = Utils.GetLogger(xunitLogger);
        }

        private async Task Execute(string fileId, Action assertAction)
        {
            _driveHive = await Utils.PrepareDrives(fileId, _logger);

            var syncEngine = new CryptoDriveSyncEngine(_driveHive.RemoteDrive, _driveHive.LocalDrive, SyncMode.TwoWay, _logger);

            // Act
            syncEngine.Start();
            await syncEngine.StopAsync();

            // Assert
            assertAction?.Invoke();
        }

        [Fact]
        public async Task CanSyncATest()
        {
            await this.Execute("/a", () =>
            {
                var hashAlgorithm = new QuickXorHash();

                Assert.True(File.Exists("/a".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/a"
                    .ToConflictFilePath(Utils.DriveItemPool["/a2"]().LastModified())
                    .ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/a".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                using (var stream = File.OpenRead("/a"
                    .ToConflictFilePath(Utils.DriveItemPool["/a2"]().LastModified())
                    .ToAbsolutePath(_driveHive.LocalDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == Utils.DriveItemPool["/a2"]().QuickXorHash());
                }
            });
        }

        [Fact]
        public async Task CanSyncBTest()
        {
            await this.Execute("/b", () =>
            {
                Assert.True(File.Exists("/b".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/b".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");
            });
        }

        [Fact]
        public async Task CanSyncCTest()
        {
            await this.Execute("/c", () =>
            {
                Assert.True(File.Exists("/c".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/c".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");
            });
        }

        [Fact]
        public async Task CanSyncDTest()
        {
            await this.Execute("/d", () =>
            {
                var hashAlgorithm = new QuickXorHash();

                Assert.True(File.Exists("/d".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/d".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                using (var stream = File.OpenRead("/d".ToAbsolutePath(_driveHive.LocalDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == Utils.DriveItemPool["/d1"]().QuickXorHash());
                }
            });
        }

        [Fact]
        public async Task CanSyncETest()
        {
            await this.Execute("/e", () =>
            {
                Assert.True(File.Exists("/e"
                    .ToConflictFilePath(Utils.DriveItemPool["/e1"]().LastModified())
                    .ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/e".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/e".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");
            });
        }

        [Fact]
        public async Task CanSyncFTest()
        {
            await this.Execute("/f", () =>
            {
                Assert.True(File.Exists("/f"
                    .ToConflictFilePath(Utils.DriveItemPool["/f1"]().LastModified())
                    .ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/f".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/f".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                Utils.CompareFiles("/f", "/f2", _driveHive.LocalDrivePath);
            });
        }

        [Fact]
        public async Task CanSyncGTest()
        {
            await this.Execute("/g", () =>
            {
                Assert.True(File.Exists("/g".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/g"
                    .ToConflictFilePath(Utils.DriveItemPool["/g1"]().LastModified())
                    .ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/g".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");
            });
        }

        [Fact]
        public async Task CanSyncHTest()
        {
            await this.Execute("/h", () =>
            {
                Assert.True(File.Exists("/h".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/h"
                    .ToConflictFilePath(Utils.DriveItemPool["/h1"]().LastModified())
                    .ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/h".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                Utils.CompareFiles("/h", "/h1", _driveHive.LocalDrivePath);
            });
        }

        [Fact]
        public async Task CanSyncITest()
        {
            await this.Execute("/i", () =>
            {
                Assert.True(File.Exists("/i"
                    .ToConflictFilePath(Utils.DriveItemPool["/i1"]().LastModified())
                    .ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/i".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/i".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                Utils.CompareFiles("/i", "/i2", _driveHive.LocalDrivePath);
            });
        }

        [Fact]
        public async Task CanSyncJTest()
        {
            await this.Execute("/j", () =>
            {
                var hashAlgorithm = new QuickXorHash();

                Assert.True(File.Exists("/j"
                    .ToConflictFilePath(Utils.DriveItemPool["/j1"]().LastModified())
                    .ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/j".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/j"
                    .ToConflictFilePath(Utils.DriveItemPool["/j3"]().LastModified())
                    .ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/j".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                using (var stream = File.OpenRead("/j"
                    .ToConflictFilePath(Utils.DriveItemPool["/j3"]().LastModified())
                    .ToAbsolutePath(_driveHive.LocalDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == Utils.DriveItemPool["/j3"]().QuickXorHash());
                }
            });
        }

        [Fact]
        public async Task CanSyncKTest()
        {
            await this.Execute("/k", () =>
            {
                Assert.True(File.Exists("/k".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/k"
                    .ToConflictFilePath(Utils.DriveItemPool["/k1"]().LastModified())
                    .ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/k".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                Utils.CompareFiles("/k", "/k1", _driveHive.LocalDrivePath);
            });
        }

        public void Dispose()
        {
            _loggerProviders.ForEach(loggerProvider => loggerProvider.Dispose());

            Directory.Delete(_driveHive.RemoteDrivePath, true);
            Directory.Delete(_driveHive.LocalDrivePath, true);
        }
    }
}