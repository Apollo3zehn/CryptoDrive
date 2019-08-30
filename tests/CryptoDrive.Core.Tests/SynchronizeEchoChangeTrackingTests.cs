using CryptoDrive.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        public SynchronizeEchoChangeTrackingTests(ITestOutputHelper xunitLogger)
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

        private async void Execute(string fileId, Func<Task> actAction, Action assertAction)
        {
            var dbName = Path.GetTempFileName();

            // Arrange
            var options = new DbContextOptionsBuilder<CryptoDriveDbContext>()
                // InMemoryDatabase is currently broken
                //.UseInMemoryDatabase(databaseName: "CryptoDrive")
                .UseSqlite($"Data Source={dbName}")
                .Options;

            _driveHive = await Utils.PrepareDrives(fileId, _logger);

            using (var context = new CryptoDriveDbContext(options))
            {
                context.Database.EnsureCreated();

                var synchronizer = new CryptoDriveSyncEngine(_driveHive.RemoteDrive, _driveHive.LocalDrive, context, _logger);
                await synchronizer.Synchronize(SyncMode.Echo);

                // Act
                await actAction?.Invoke();
                await synchronizer.Synchronize(SyncMode.Echo);

                // Assert
                assertAction?.Invoke();
            }
        }

        [Fact]
        public void CanSyncSubA_AddTest()
        {
            this.Execute("sub/a", async () =>
            {
                /* add new local file */
                await _driveHive.LocalDrive.CreateOrUpdateAsync(Utils.DriveItemPool["b1"]);
            },
            () =>
            {
                var hashAlgorithm = new QuickXorHash();

                Assert.True(File.Exists("sub/a".ToAbsolutePath(_driveHive.LocalDrivePath)));
                Assert.True(File.Exists("sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)));
                Assert.True(File.Exists("b".ToAbsolutePath(_driveHive.RemoteDrivePath)));

                using (var stream = File.OpenRead("sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == Utils.DriveItemPool["sub/a1"].QuickXorHash());
                }
            });
        }

        [Fact]
        public void CanSyncSubA_DeleteTest()
        {
            this.Execute("sub/a", async () =>
            {
                /* delete local file */
                await _driveHive.LocalDrive.DeleteAsync(Utils.DriveItemPool["sub/a1"]);
            },
            () =>
            {
                var hashAlgorithm = new QuickXorHash();

                Assert.True(!File.Exists("sub/a".ToAbsolutePath(_driveHive.LocalDrivePath)));
                Assert.True(!File.Exists("sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)));
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