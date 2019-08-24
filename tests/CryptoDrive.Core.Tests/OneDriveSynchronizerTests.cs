using CryptoDrive.Extensions;
using CryptoDrive.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace CryptoDrive.Core.Tests
{
    public class OneDriveSynchronizerTests : IDisposable
    {
        private List<ILoggerProvider> _loggerProviders;
        private ILogger<CryptoDriveSyncEngine> _logger;

        private string _localDrivePath;
        private string _remoteDrivePath;
        private Dictionary<string, DriveItem> _driveItemPool;

        public OneDriveSynchronizerTests(ITestOutputHelper xunitLogger)
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

        [Fact]
        public async void SynchronizeTheUniverseTest()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CryptoDriveDbContext>()
                // InMemoryDatabase is currently broken
                //.UseInMemoryDatabase(databaseName: "CryptoDrive")
                .UseSqlite($"Data Source={Path.GetTempFileName()}")
                .Options;

            (var remoteDrive, var localDrive) = await this.PrepareDrives();

            using (var context = new CryptoDriveDbContext(options))
            {
                context.Database.EnsureCreated();

                var synchronizer = new CryptoDriveSyncEngine(remoteDrive, localDrive, context, _logger);
                var hashAlgorithm = new QuickXorHash();

                // Act
                _logger.LogInformation("Test started.");
                await synchronizer.SynchronizeTheUniverse();
                _logger.LogInformation("Test finished.");

                // Assert

                // file a
                Assert.True(File.Exists("a".ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("a"
                    .ToConflictFilePath(_driveItemPool["a2"].LastModified())
                    .ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("a".ToAbsolutePath(_remoteDrivePath)));

                using (var stream = File.OpenRead("a"
                    .ToConflictFilePath(_driveItemPool["a2"].LastModified())
                    .ToAbsolutePath(_localDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == _driveItemPool["a2"].CTag);
                }

                // file b
                Assert.True(File.Exists("b".ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("b".ToAbsolutePath(_remoteDrivePath)));

                // file c
                Assert.True(File.Exists("c".ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("c".ToAbsolutePath(_remoteDrivePath)));

                using (var stream = File.OpenRead("c".ToAbsolutePath(_localDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == _driveItemPool["c1"].CTag);
                }

                // file d
                Assert.True(File.Exists("d".ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("d".ToAbsolutePath(_remoteDrivePath)));

                using (var stream = File.OpenRead("d".ToAbsolutePath(_localDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == _driveItemPool["d1"].CTag);
                }

                // file e
                Assert.True(File.Exists("e"
                    .ToConflictFilePath(_driveItemPool["e1"].LastModified())
                    .ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("e".ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("e".ToAbsolutePath(_remoteDrivePath)));

                // file f
                Assert.True(File.Exists("f"
                    .ToConflictFilePath(_driveItemPool["f1"].LastModified())
                    .ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("f".ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("f".ToAbsolutePath(_remoteDrivePath)));
                
                using (var stream = File.OpenRead("f".ToAbsolutePath(_localDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == _driveItemPool["f2"].CTag);
                }

                // file g
                Assert.True(File.Exists("g".ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("g"
                    .ToConflictFilePath(_driveItemPool["g1"].LastModified())
                    .ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("g".ToAbsolutePath(_remoteDrivePath)));

                // file h
                Assert.True(File.Exists("h".ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("h"
                    .ToConflictFilePath(_driveItemPool["h1"].LastModified())
                    .ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("h".ToAbsolutePath(_remoteDrivePath)));

                using (var stream = File.OpenRead("h"
                    .ToAbsolutePath(_localDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == _driveItemPool["h1"].CTag);
                }

                // file i
                Assert.True(File.Exists("i"
                    .ToConflictFilePath(_driveItemPool["i1"].LastModified())
                    .ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("i".ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("i".ToAbsolutePath(_remoteDrivePath)));

                using (var stream = File.OpenRead("i"
                    .ToAbsolutePath(_localDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == _driveItemPool["i2"].CTag);
                }

                // file j
                Assert.True(File.Exists("j"
                    .ToConflictFilePath(_driveItemPool["j1"].LastModified())
                    .ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("j".ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("j"
                    .ToConflictFilePath(_driveItemPool["j3"].LastModified())
                    .ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("j".ToAbsolutePath(_remoteDrivePath)));

                using (var stream = File.OpenRead("j"
                    .ToConflictFilePath(_driveItemPool["j3"].LastModified())
                    .ToAbsolutePath(_localDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == _driveItemPool["j3"].CTag);
                }

                // file k
                Assert.True(File.Exists("k".ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("k"
                    .ToConflictFilePath(_driveItemPool["k1"].LastModified())
                    .ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("k".ToAbsolutePath(_remoteDrivePath)));

                using (var stream = File.OpenRead("k".ToAbsolutePath(_localDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == _driveItemPool["k1"].CTag);
                }

                // remote
            }
        }

        private async Task<(IDriveProxy, IDriveProxy)> PrepareDrives()
        {
            _remoteDrivePath = Path.Combine(Path.GetTempPath(), "CryptoDriveRemote_" + Path.GetRandomFileName().Replace(".", string.Empty));
            var remoteDrive = new LocalDriveProxy(_remoteDrivePath, "OneDrive", _logger);

            _localDrivePath = Path.Combine(Path.GetTempPath(), "CryptoDriveLocal_" + Path.GetRandomFileName().Replace(".", string.Empty));
            var localDrive = new LocalDriveProxy(_localDrivePath, "local", _logger);

            _driveItemPool = new Dictionary<string, DriveItem>()
            {
                ["a1"] = this.CreateDriveItem("a", 1),
                ["a2"] = this.CreateDriveItem("a", 2),

                ["b1"] = this.CreateDriveItem("b", 1),

                ["c1"] = this.CreateDriveItem("c", 1),

                ["d1"] = this.CreateDriveItem("d", 1),

                ["e1"] = this.CreateDriveItem("e", 1),
                ["e2"] = this.CreateDriveItem("e", 2),

                ["f1"] = this.CreateDriveItem("f", 1),
                ["f2"] = this.CreateDriveItem("f", 2),

                ["g1"] = this.CreateDriveItem("g", 1),

                ["h1"] = this.CreateDriveItem("h", 1),

                ["i1"] = this.CreateDriveItem("i", 1),
                ["i2"] = this.CreateDriveItem("i", 2),

                ["j1"] = this.CreateDriveItem("j", 1),
                ["j2"] = this.CreateDriveItem("j", 2),
                ["j3"] = this.CreateDriveItem("j", 3),

                ["k1"] = this.CreateDriveItem("k", 1),
            };

            // _driveItemLocal
            await localDrive.CreateOrUpdateAsync(_driveItemPool["a1"]);

            await localDrive.CreateOrUpdateAsync(_driveItemPool["b1"]);

            await localDrive.CreateOrUpdateAsync(_driveItemPool["d1"]);

            await localDrive.CreateOrUpdateAsync(_driveItemPool["e1"].MemberwiseClone().ToConflict());
            await localDrive.CreateOrUpdateAsync(_driveItemPool["e2"]);

            await localDrive.CreateOrUpdateAsync(_driveItemPool["f1"].MemberwiseClone().ToConflict());
            await localDrive.CreateOrUpdateAsync(_driveItemPool["f2"]);

            await localDrive.CreateOrUpdateAsync(_driveItemPool["g1"]);
            await localDrive.CreateOrUpdateAsync(_driveItemPool["g1"].MemberwiseClone().ToConflict());

            await localDrive.CreateOrUpdateAsync(_driveItemPool["h1"].MemberwiseClone().ToConflict());

            await localDrive.CreateOrUpdateAsync(_driveItemPool["i1"].MemberwiseClone().ToConflict());

            await localDrive.CreateOrUpdateAsync(_driveItemPool["j1"].MemberwiseClone().ToConflict());
            await localDrive.CreateOrUpdateAsync(_driveItemPool["j2"]);

            await localDrive.CreateOrUpdateAsync(_driveItemPool["k1"]);
            await localDrive.CreateOrUpdateAsync(_driveItemPool["k1"].MemberwiseClone().ToConflict());

            // _remoteDrive
            await remoteDrive.CreateOrUpdateAsync(_driveItemPool["a2"]);
            await remoteDrive.CreateOrUpdateAsync(_driveItemPool["b1"]);
            await remoteDrive.CreateOrUpdateAsync(_driveItemPool["c1"]);
            await remoteDrive.CreateOrUpdateAsync(_driveItemPool["e1"]);
            await remoteDrive.CreateOrUpdateAsync(_driveItemPool["g1"]);
            await remoteDrive.CreateOrUpdateAsync(_driveItemPool["h1"]);
            await remoteDrive.CreateOrUpdateAsync(_driveItemPool["i2"]);
            await remoteDrive.CreateOrUpdateAsync(_driveItemPool["j3"]);

            return (remoteDrive, localDrive);
        }

        private DriveItem CreateDriveItem(string name, int version)
        {
            var hashAlgorithm = new QuickXorHash();
            var lastModified = new DateTime(2019, 01, 01, version, 00, 00, DateTimeKind.Utc);
            var content = $"{name} v{version}".ToMemorySteam();

            return new DriveItem
            {
                File = new Microsoft.Graph.File(),
                Name = name,
                Content = content,
                CTag = Convert.ToBase64String(hashAlgorithm.ComputeHash(content)),
                FileSystemInfo = new Microsoft.Graph.FileSystemInfo { LastModifiedDateTime = lastModified },
                ParentReference = new ItemReference() { Path = CryptoDriveConstants.PathPrefix }
            };
        }

        public void Dispose()
        {
            _loggerProviders.ForEach(loggerProvider => loggerProvider.Dispose());

            Directory.Delete(_localDrivePath, true);
            Directory.Delete(_remoteDrivePath, true);
        }
    }
}