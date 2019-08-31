using CryptoDrive.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

                var syncEngine = new CryptoDriveSyncEngine(_driveHive.RemoteDrive, _driveHive.LocalDrive, context, SyncMode.Echo, _logger);
                syncEngine.Start();

                // Act
                await Task.Delay(TimeSpan.FromSeconds(5));
                await actAction?.Invoke();
                await Task.Delay(TimeSpan.FromSeconds(1));
                await syncEngine.Stop();

                // Assert
                assertAction?.Invoke();

                _driveHive.Dispose();
            }
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
        public void CanSyncSubA_AddTest()
        {
            this.Execute("sub/a", async () =>
            {
                /* add new file */
                await _driveHive.LocalDrive.CreateOrUpdateAsync(Utils.DriveItemPool["b1"]);
            },
            () =>
            {
                Assert.True(File.Exists("sub/a".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");
                Assert.True(File.Exists("b".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                this.CompareFiles("sub/a", "sub/a1", _driveHive.RemoteDrivePath);
            });
        }

        [Fact]
        public void CanSyncSubA_DeleteTest()
        {
            this.Execute("sub/a", async () =>
            {
                /* delete file */
                await _driveHive.LocalDrive.DeleteAsync(Utils.DriveItemPool["sub/a1"]);
            },
            () =>
            {
                Assert.True(!File.Exists("sub/a".ToAbsolutePath(_driveHive.LocalDrivePath)), "File should not exist.");
                Assert.True(!File.Exists("sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File should not exist.");
            });
        }

        [Fact]
        public void CanSyncSubA_ModifyTest()
        {
            this.Execute("sub/a", async () =>
            {
                var driveItem = Utils.DriveItemPool["sub/a1"].MemberwiseClone();

                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("New content.")))
                {
                    driveItem.Content = stream;
                    driveItem.FileSystemInfo.LastModifiedDateTime = DateTime.UtcNow;

                    /* modify file */
                    await _driveHive.LocalDrive.CreateOrUpdateAsync(driveItem);
                }
            },
            () =>
            {
                var hashAlgorithm = new QuickXorHash();

                Assert.True(File.Exists("sub/a".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");

                using (var stream = File.OpenRead("sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)))
                {
                    var actual = Convert.ToBase64String(hashAlgorithm.ComputeHash(stream));
                    var expected = Convert.ToBase64String(hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes("New content.")));

                    Assert.True(actual == expected, "The hashes are not equal.");
                }
            });
        }

        [Fact]
        public void CanSyncSubA_Move1Test()
        {
            this.Execute("sub/a", async () =>
            {
                var newDriveItem = Utils.DriveItemPool["sub/a1"].MemberwiseClone();
                newDriveItem.ParentReference.Path = newDriveItem.ParentReference.Path.Replace("sub", "sub_new");

                /* move file to new folder */
                await _driveHive.LocalDrive.MoveAsync(Utils.DriveItemPool["sub/a1"], newDriveItem);
            },
            () =>
            {
                Assert.True(File.Exists("sub_new/a".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("sub_new/a".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");
            });
        }

        [Fact]
        public void CanSyncSubA_Move2Test()
        {
            this.Execute("sub/a", async () =>
            {
                var newDriveItem = Utils.DriveItemPool["sub/a1"].MemberwiseClone();
                newDriveItem.Name = newDriveItem.Name.Replace("a", "a_new");

                /* rename file */
                await _driveHive.LocalDrive.MoveAsync(Utils.DriveItemPool["sub/a1"], newDriveItem);
            },
            () =>
            {
                Assert.True(File.Exists("sub/a_new".ToAbsolutePath(_driveHive.LocalDrivePath)), "File does not exist.");
                Assert.True(File.Exists("sub/a_new".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");
            });
        }

        [Fact]
        public void CanSyncSubA_Move3Test()
        {
            this.Execute("sub/a", async () =>
            {
                var oldDriveItem = "sub".ToDriveItem(DriveItemType.Folder);
                var newDriveItem = oldDriveItem.MemberwiseClone();

                newDriveItem.Name = newDriveItem.Name.Replace("sub", "sub_new");

                /* rename folder */
                await _driveHive.LocalDrive.MoveAsync(oldDriveItem, newDriveItem);
            },
            () =>
            {
                Assert.True(Directory.Exists("sub_new".ToAbsolutePath(_driveHive.LocalDrivePath)), "Folder does not exist.");
                Assert.True(Directory.Exists("sub_new".ToAbsolutePath(_driveHive.RemoteDrivePath)), "Folder does not exist.");
            });
        }

        [Fact]
        public void CanSyncExtA_MoveTest()
        {
            this.Execute("", async () =>
            {
                var externalDrivePath = Path.Combine(Path.GetTempPath(), "CryptoDriveExternal_" + Guid.NewGuid().ToString());
                var externalDrive = new LocalDriveProxy(externalDrivePath, "External", _logger);

                await externalDrive.CreateOrUpdateAsync(Utils.DriveItemPool["sub/a1"]);

                /* move folder from external drive to local drive */
                var newDriveItem = Utils.DriveItemPool["sub/a1"].MemberwiseClone();
                var sourcePath = Path.GetDirectoryName(newDriveItem.GetAbsolutePath(externalDrivePath));
                var targetPath = Path.GetDirectoryName(newDriveItem.GetAbsolutePath(_driveHive.LocalDrivePath));

                Directory.Move(sourcePath, targetPath);
            },
            () =>
            {
                Assert.True(Directory.Exists("sub".ToAbsolutePath(_driveHive.RemoteDrivePath)), "Folder does not exist.");
                Assert.True(File.Exists("sub/a".ToAbsolutePath(_driveHive.RemoteDrivePath)), "File does not exist.");
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