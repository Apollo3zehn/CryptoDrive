using CryptoDrive.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace CryptoDrive.Core.Tests
{
    public class SynchronizeEchoFirstTimeTests : IDisposable
    {
        private List<ILoggerProvider> _loggerProviders;
        private ILogger<CryptoDriveSyncEngine> _logger;

        private DriveHive _driveHive;

        public SynchronizeEchoFirstTimeTests(ITestOutputHelper xunitLogger)
        {
            (_logger, _loggerProviders) = Utils.GetLogger(xunitLogger);
        }

        private async Task Execute(string fileId, Action assertAction)
        {
            // Arrange
            Exception ex = null;
            _driveHive = await Utils.PrepareDrives(fileId, _logger);
            var syncEngine = new CryptoDriveSyncEngine(_driveHive.RemoteDrive, _driveHive.LocalDrive, _logger);
            var manualReset = new ManualResetEventSlim();

            syncEngine.SyncCompleted += (sender, e) =>
            {
                if (ex == null && e.Exception != null)
                    ex = e.Exception;

                try
                {
                    if (e.SyncId == 0)
                        _ = syncEngine.StopAsync();
                }
                finally
                {
                    manualReset.Set();
                }
            };

            // Act
            syncEngine.Start();
            manualReset.Wait(timeout: TimeSpan.FromSeconds(30));

            // Assert
            assertAction?.Invoke();

            if (ex != null)
                throw ex;

            _driveHive.Dispose();
        }

        [Fact]
        public async Task CanSyncATest()
        {
            await this.Execute("/a", () =>
            {
                Assert.True(File.Exists("/a".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/a".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                Utils.CompareFiles("/a", "/a1", _driveHive.RemoteDrivePath);
            });
        }

        [Fact]
        public async Task CanSyncSubATest()
        {
            await this.Execute("/sub/a", () =>
            {
                Assert.True(File.Exists("/sub/a".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                Utils.CompareFiles("/sub/a", "/sub/a1", _driveHive.RemoteDrivePath);
            });
        }

        [Fact]
        public async Task CanSyncBTest()
        {
            await this.Execute("/b", () =>
            {
                Assert.True(File.Exists("/b".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/b".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                Utils.CompareFiles("/b", "/b1", _driveHive.RemoteDrivePath);
            });
        }

        [Fact]
        public async Task CanSyncCTest()
        {
            await this.Execute("/c", () =>
            {
                Assert.True(!File.Exists("/c".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File should not exist.");
            });
        }

        [Fact]
        public async Task CanSyncDTest()
        {
            await this.Execute("/d", () =>
            {
                Assert.True(File.Exists("/d".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/d".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                Utils.CompareFiles("/d", "/d1", _driveHive.RemoteDrivePath);
            });
        }

        [Fact]
        public async Task CanSyncETest()
        {
            await this.Execute("/e", () =>
            {
                Assert.True(File.Exists("/e".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/e".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                Utils.CompareFiles("/e", "/e2", _driveHive.RemoteDrivePath);
            });
        }

        [Fact]
        public async Task CanSyncFTest()
        {
            await this.Execute("/f", () =>
            {
                Assert.True(File.Exists("/f".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/f".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                Utils.CompareFiles("/f", "/f2", _driveHive.RemoteDrivePath);
            });
        }

        [Fact]
        public async Task CanSyncGTest()
        {
            await this.Execute("/g", () =>
            {
                Assert.True(File.Exists("/g".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/g".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                Utils.CompareFiles("/g", "/g1", _driveHive.RemoteDrivePath);
            });
        }

        [Fact]
        public async Task CanSyncHTest()
        {
            await this.Execute("/h", () =>
            {
                Assert.True(!File.Exists("/h".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File should not exist.");
            });
        }

        [Fact]
        public async Task CanSyncITest()
        {
            await this.Execute("/i", () =>
            {
                Assert.True(!File.Exists("/i".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File should not exist.");
            });
        }

        [Fact]
        public async Task CanSyncJTest()
        {
            await this.Execute("/j", () =>
            {
                Assert.True(File.Exists("/j".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/j".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                Utils.CompareFiles("/j", "/j2", _driveHive.RemoteDrivePath);
            });
        }

        [Fact]
        public async Task CanSyncKTest()
        {
            await this.Execute("/k", () =>
            {
                Assert.True(File.Exists("/k".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("/k".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                Utils.CompareFiles("/k", "/k1", _driveHive.RemoteDrivePath);
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