using CryptoDrive.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            // logger
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddLogging(loggingBuilder =>
            {
                loggingBuilder
                .AddSeq()
                .AddProvider(new XunitLoggerProvider(xunitLogger))
                .SetMinimumLevel(LogLevel.Trace);

                _loggerProviders = loggingBuilder.Services
                    .Where(descriptor => typeof(ILoggerProvider).IsAssignableFrom(descriptor.ImplementationInstance?.GetType()))
                    .Select(descriptor => (ILoggerProvider)descriptor.ImplementationInstance)
                    .ToList();
            });

            var serviceProvider = serviceCollection.BuildServiceProvider();
            _logger = serviceProvider.GetService<ILogger<CryptoDriveSyncEngine>>();
        }

        private async void Execute(string fileId, Action assertAction)
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CryptoDriveDbContext>()
                // InMemoryDatabase is currently broken
                //.UseInMemoryDatabase(databaseName: "CryptoDrive")
                .UseSqlite($"Data Source={Path.GetTempFileName()}")
                .Options;

            _driveHive = await Utils.PrepareDrives(fileId, _logger);

            using (var context = new CryptoDriveDbContext(options))
            {
                context.Database.EnsureCreated();

                var synchronizer = new CryptoDriveSyncEngine(_driveHive.RemoteDrive, _driveHive.LocalDrive, context, _logger);

                // Act
                await synchronizer.Synchronize(SyncMode.Echo);

                // Assert
                assertAction?.Invoke();
            }
        }

        [Fact]
        public void CanSyncATest()
        {
            this.Execute("a", () =>
            {
                var hashAlgorithm = new QuickXorHash();

                Assert.True(File.Exists("a".ToAbsolutePath(_driveHive.LocalDrivePath)));
                Assert.True(File.Exists("a".ToAbsolutePath(_driveHive.RemoteDrivePath)));

                using (var stream = File.OpenRead("a".ToAbsolutePath(_driveHive.RemoteDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == Utils.DriveItemPool["a1"].QuickXorHash());
                }
            });
        }

        [Fact]
        public void CanSyncSubATest()
        {
            this.Execute("sub/a", () =>
            {
                var hashAlgorithm = new QuickXorHash();

                Assert.True(File.Exists("sub/a".ToAbsolutePath(_driveHive.LocalDrivePath)));
                Assert.True(File.Exists("sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)));

                using (var stream = File.OpenRead("sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == Utils.DriveItemPool["sub/a1"].QuickXorHash());
                }
            });
        }

        [Fact]
        public void CanSyncBTest()
        {
            this.Execute("b", () =>
            {
                var hashAlgorithm = new QuickXorHash();

                Assert.True(File.Exists("b".ToAbsolutePath(_driveHive.LocalDrivePath)));
                Assert.True(File.Exists("b".ToAbsolutePath(_driveHive.RemoteDrivePath)));

                using (var stream = File.OpenRead("b".ToAbsolutePath(_driveHive.RemoteDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == Utils.DriveItemPool["b1"].QuickXorHash());
                }
            });
        }

        [Fact]
        public void CanSyncCTest()
        {
            this.Execute("c", () =>
            {
                Assert.True(!File.Exists("c".ToAbsolutePath(_driveHive.RemoteDrivePath)));
            });
        }

        [Fact]
        public void CanSyncDTest()
        {
            this.Execute("d", () =>
            {
                var hashAlgorithm = new QuickXorHash();

                Assert.True(File.Exists("d".ToAbsolutePath(_driveHive.LocalDrivePath)));
                Assert.True(File.Exists("d".ToAbsolutePath(_driveHive.RemoteDrivePath)));

                using (var stream = File.OpenRead("d".ToAbsolutePath(_driveHive.RemoteDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == Utils.DriveItemPool["d1"].QuickXorHash());
                }
            });
        }

        [Fact]
        public void CanSyncETest()
        {
            this.Execute("e", () =>
            {
                var hashAlgorithm = new QuickXorHash();

                Assert.True(File.Exists("e".ToAbsolutePath(_driveHive.LocalDrivePath)));
                Assert.True(File.Exists("e".ToAbsolutePath(_driveHive.RemoteDrivePath)));

                using (var stream = File.OpenRead("e".ToAbsolutePath(_driveHive.RemoteDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == Utils.DriveItemPool["e2"].QuickXorHash());
                }
            });
        }

        [Fact]
        public void CanSyncFTest()
        {
            this.Execute("f", () =>
            {
                var hashAlgorithm = new QuickXorHash();

                Assert.True(File.Exists("f".ToAbsolutePath(_driveHive.LocalDrivePath)));
                Assert.True(File.Exists("f".ToAbsolutePath(_driveHive.RemoteDrivePath)));

                using (var stream = File.OpenRead("f".ToAbsolutePath(_driveHive.RemoteDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == Utils.DriveItemPool["f2"].QuickXorHash());
                }
            });
        }

        [Fact]
        public void CanSyncGTest()
        {
            this.Execute("g", () =>
            {
                var hashAlgorithm = new QuickXorHash();

                Assert.True(File.Exists("g".ToAbsolutePath(_driveHive.LocalDrivePath)));
                Assert.True(File.Exists("g".ToAbsolutePath(_driveHive.RemoteDrivePath)));

                using (var stream = File.OpenRead("g".ToAbsolutePath(_driveHive.RemoteDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == Utils.DriveItemPool["g1"].QuickXorHash());
                }
            });
        }

        [Fact]
        public void CanSyncHTest()
        {
            this.Execute("h", () =>
            {
                Assert.True(!File.Exists("h".ToAbsolutePath(_driveHive.RemoteDrivePath)));
            });
        }

        [Fact]
        public void CanSyncITest()
        {
            this.Execute("i", () =>
            {
                Assert.True(!File.Exists("i".ToAbsolutePath(_driveHive.RemoteDrivePath)));
            });
        }

        [Fact]
        public void CanSyncJTest()
        {
            this.Execute("j", () =>
            {
                var hashAlgorithm = new QuickXorHash();

                Assert.True(File.Exists("j".ToAbsolutePath(_driveHive.LocalDrivePath)));
                Assert.True(File.Exists("j".ToAbsolutePath(_driveHive.RemoteDrivePath)));

                using (var stream = File.OpenRead("j".ToAbsolutePath(_driveHive.RemoteDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == Utils.DriveItemPool["j2"].QuickXorHash());
                }
            });
        }

        [Fact]
        public void CanSyncKTest()
        {
            this.Execute("k", () =>
            {
                var hashAlgorithm = new QuickXorHash();

                Assert.True(File.Exists("k".ToAbsolutePath(_driveHive.LocalDrivePath)));
                Assert.True(File.Exists("k".ToAbsolutePath(_driveHive.RemoteDrivePath)));

                using (var stream = File.OpenRead("k".ToAbsolutePath(_driveHive.RemoteDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == Utils.DriveItemPool["k1"].QuickXorHash());
                }
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