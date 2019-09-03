using CryptoDrive.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
using Directory = System.IO.Directory;
using File = System.IO.File;

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

        private async void Execute(string fileId, Action assertAction)
        {
            _driveHive = await Utils.PrepareDrives(fileId, _logger);

            var syncEngine = new CryptoDriveSyncEngine(_driveHive.RemoteDrive, _driveHive.LocalDrive, SyncMode.Echo, _logger);

            // Act
            syncEngine.Start();
            await syncEngine.StopAsync();

            // Assert
            assertAction?.Invoke();
        }

        private void CompareFiles(string fileName, string versionName, string basePath)
        {
            var hashAlgorithm = new QuickXorHash();

            using (var stream = File.OpenRead(fileName.ToAbsolutePath(basePath)))
            {
                var actual = Convert.ToBase64String(hashAlgorithm.ComputeHash(stream));
                var expected = Utils.DriveItemPool[versionName].QuickXorHash();

                Assert.True(actual == expected, "The hashes are not equal.");
            }
        }

        [Fact]
        public void CanSyncATest()
        {
            this.Execute("a", () =>
            {
                Assert.True(File.Exists("a".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("a".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                this.CompareFiles("a", "a1", _driveHive.RemoteDrivePath);
            });
        }

        [Fact]
        public void CanSyncSubATest()
        {
            this.Execute("sub/a", () =>
            {
                Assert.True(File.Exists("sub/a".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                this.CompareFiles("sub/a", "sub/a1", _driveHive.RemoteDrivePath);
            });
        }

        [Fact]
        public void CanSyncBTest()
        {
            this.Execute("b", () =>
            {
                Assert.True(File.Exists("b".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("b".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                this.CompareFiles("b", "b1", _driveHive.RemoteDrivePath);
            });
        }

        [Fact]
        public void CanSyncCTest()
        {
            this.Execute("c", () =>
            {
                Assert.True(!File.Exists("c".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File should not exist.");
            });
        }

        [Fact]
        public void CanSyncDTest()
        {
            this.Execute("d", () =>
            {
                Assert.True(File.Exists("d".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("d".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                this.CompareFiles("d", "d1", _driveHive.RemoteDrivePath);
            });
        }

        [Fact]
        public void CanSyncETest()
        {
            this.Execute("e", () =>
            {
                Assert.True(File.Exists("e".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("e".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                this.CompareFiles("e", "e2", _driveHive.RemoteDrivePath);
            });
        }

        [Fact]
        public void CanSyncFTest()
        {
            this.Execute("f", () =>
            {
                Assert.True(File.Exists("f".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("f".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                this.CompareFiles("f", "f2", _driveHive.RemoteDrivePath);
            });
        }

        [Fact]
        public void CanSyncGTest()
        {
            this.Execute("g", () =>
            {
                Assert.True(File.Exists("g".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("g".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                this.CompareFiles("g", "g1", _driveHive.RemoteDrivePath);
            });
        }

        [Fact]
        public void CanSyncHTest()
        {
            this.Execute("h", () =>
            {
                Assert.True(!File.Exists("h".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File should not exist.");
            });
        }

        [Fact]
        public void CanSyncITest()
        {
            this.Execute("i", () =>
            {
                Assert.True(!File.Exists("i".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File should not exist.");
            });
        }

        [Fact]
        public void CanSyncJTest()
        {
            this.Execute("j", () =>
            {
                Assert.True(File.Exists("j".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("j".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                this.CompareFiles("j", "j2", _driveHive.RemoteDrivePath);
            });
        }

        [Fact]
        public void CanSyncKTest()
        {
            this.Execute("k", () =>
            {
                Assert.True(File.Exists("k".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("k".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                this.CompareFiles("k", "k1", _driveHive.RemoteDrivePath);
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