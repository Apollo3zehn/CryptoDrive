using CryptoDrive.Extensions;
using CryptoDrive.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Moq;
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
        private IOneDriveClient _oneDriveClient;

        private List<ILoggerProvider> _loggerProviders;
        private ILogger<OneDriveSynchronizer> _logger;

        private string _localDrivePath;
        private string _remoteDrivePath;
        private RemoteTestDrive _remoteDrive;
        private Dictionary<string, DriveItem> _driveItemPool;

        public OneDriveSynchronizerTests(ITestOutputHelper xunitLogger)
        {
            // directory
            _localDrivePath = Path.Combine(Path.GetTempPath(), "CryptoDriveLocal_" + Path.GetRandomFileName().Replace(".", string.Empty));
            _remoteDrivePath = Path.Combine(Path.GetTempPath(), "CryptoDriveRemote_" + Path.GetRandomFileName().Replace(".", string.Empty));
            
            Directory.CreateDirectory(_localDrivePath);
            Directory.CreateDirectory(_remoteDrivePath);

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
            _logger = serviceProvider.GetService<ILogger<OneDriveSynchronizer>>();

            // _graphClient
            var oneDriveClientMock = new Mock<IOneDriveClient>();

            oneDriveClientMock
                .Setup(x => x.GetDeltaPageAsync())
                .Returns(() => Task.FromResult((_remoteDrive.GetDelta(), true)));

            oneDriveClientMock
                .Setup(x => x.GetDownloadUrlAsync(It.IsAny<string>()))
                .Returns<string>(id => Task.FromResult(_remoteDrive.GetDownloadUrl(id)));

            oneDriveClientMock
                .Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((filePath, rootFolderPath) =>
            {
                var localFilePath = filePath.ToAbsolutePath(rootFolderPath);

                using (var stream = File.OpenRead(localFilePath))
                {
                    return Task.FromResult(_remoteDrive.Upload(filePath, stream, File.GetLastWriteTimeUtc(localFilePath)));
                }
            });

            _oneDriveClient = oneDriveClientMock.Object;
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

            this.PrepareDrives();

            using (var context = new CryptoDriveDbContext(options))
            {
                context.Database.EnsureCreated();

                var synchronizer = new OneDriveSynchronizer(_localDrivePath, _oneDriveClient, context, _logger);
                var hashAlgorithm = new QuickXorHash();

                // Act
                await synchronizer.SynchronizeTheUniverse();

                _logger.LogInformation("Test finished.");

                // Assert

                // file a
                Assert.True(File.Exists("a".ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("a"
                    .ToConflictFilePath(_driveItemPool["a2"].FileSystemInfo.LastModifiedDateTime.Value)
                    .ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("a".ToAbsolutePath(_remoteDrivePath)));

                using (var stream = File.OpenRead("a"
                    .ToConflictFilePath(_driveItemPool["a2"].FileSystemInfo.LastModifiedDateTime.Value)
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

                using (var stream = File.OpenRead("c"
                    .ToAbsolutePath(_localDrivePath)))
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
                    .ToConflictFilePath(_driveItemPool["e1"].FileSystemInfo.LastModifiedDateTime.Value)
                    .ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("e".ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("e".ToAbsolutePath(_remoteDrivePath)));

                // file f
                Assert.True(File.Exists("f"
                    .ToConflictFilePath(_driveItemPool["f1"].FileSystemInfo.LastModifiedDateTime.Value)
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
                    .ToConflictFilePath(_driveItemPool["g1"].FileSystemInfo.LastModifiedDateTime.Value)
                    .ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("g".ToAbsolutePath(_remoteDrivePath)));

                // file h
                Assert.True(File.Exists("h".ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("h"
                    .ToConflictFilePath(_driveItemPool["h1"].FileSystemInfo.LastModifiedDateTime.Value)
                    .ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("h".ToAbsolutePath(_remoteDrivePath)));

                using (var stream = File.OpenRead("h"
                    .ToAbsolutePath(_localDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == _driveItemPool["h1"].CTag);
                }

                // file i
                Assert.True(File.Exists("i"
                    .ToConflictFilePath(_driveItemPool["i1"].FileSystemInfo.LastModifiedDateTime.Value)
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
                    .ToConflictFilePath(_driveItemPool["j1"].FileSystemInfo.LastModifiedDateTime.Value)
                    .ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("j".ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("j"
                    .ToConflictFilePath(_driveItemPool["j3"].FileSystemInfo.LastModifiedDateTime.Value)
                    .ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("j".ToAbsolutePath(_remoteDrivePath)));

                using (var stream = File.OpenRead("j"
                    .ToConflictFilePath(_driveItemPool["j3"].FileSystemInfo.LastModifiedDateTime.Value)
                    .ToAbsolutePath(_localDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == _driveItemPool["j3"].CTag);
                }

                // file k
                Assert.True(File.Exists("k".ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("k"
                    .ToConflictFilePath(_driveItemPool["k1"].FileSystemInfo.LastModifiedDateTime.Value)
                    .ToAbsolutePath(_localDrivePath)));
                Assert.True(File.Exists("k".ToAbsolutePath(_remoteDrivePath)));

                using (var stream = File.OpenRead("k".ToAbsolutePath(_localDrivePath)))
                {
                    Assert.True(Convert.ToBase64String(hashAlgorithm.ComputeHash(stream)) == _driveItemPool["k1"].CTag);
                }

                // remote
            }
        }

        private void PrepareDrives()
        {
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
            this.CreateLocalFile(_driveItemPool["a1"]);

            this.CreateLocalFile(_driveItemPool["b1"]);

            this.CreateLocalFile(_driveItemPool["d1"]);

            this.CreateLocalFile(_driveItemPool["e1"], isConflicted: true);
            this.CreateLocalFile(_driveItemPool["e2"]);

            this.CreateLocalFile(_driveItemPool["f1"], isConflicted: true);
            this.CreateLocalFile(_driveItemPool["f2"]);

            this.CreateLocalFile(_driveItemPool["g1"]);
            this.CreateLocalFile(_driveItemPool["g1"], isConflicted: true);

            this.CreateLocalFile(_driveItemPool["h1"], isConflicted: true);

            this.CreateLocalFile(_driveItemPool["i1"], isConflicted: true);

            this.CreateLocalFile(_driveItemPool["j1"], isConflicted: true);
            this.CreateLocalFile(_driveItemPool["j2"]);

            this.CreateLocalFile(_driveItemPool["k1"]);
            this.CreateLocalFile(_driveItemPool["k1"], isConflicted: true);

            // _remoteDrive
            _remoteDrive = new RemoteTestDrive(_remoteDrivePath);

            _remoteDrive.Upload(_driveItemPool["a2"]);
            _remoteDrive.Upload(_driveItemPool["b1"]);
            _remoteDrive.Upload(_driveItemPool["c1"]);
            _remoteDrive.Upload(_driveItemPool["e1"]);
            _remoteDrive.Upload(_driveItemPool["g1"]);
            _remoteDrive.Upload(_driveItemPool["h1"]);
            _remoteDrive.Upload(_driveItemPool["i2"]);
            _remoteDrive.Upload(_driveItemPool["j3"]);
        }

        private DriveItem CreateDriveItem(string name, int version)
        {
            var hashAlgorithm = new QuickXorHash();
            var lastModified = new DateTime(2019, 01, 01, version, 00, 00, DateTimeKind.Utc);
            var content = $"{name} v{version}".ToMemorySteam();

            return new DriveItem
            {
                Name = name,
                Content = content,
                CTag = Convert.ToBase64String(hashAlgorithm.ComputeHash(content)),
                FileSystemInfo = new Microsoft.Graph.FileSystemInfo { LastModifiedDateTime = lastModified },
                ParentReference = new ItemReference() { Path = CryptoDriveConstants.PathPrefix }
            };
        }

        private void CreateLocalFile(DriveItem driveItem, bool isConflicted = false)
        {
            string name;

            if (isConflicted)
                name = driveItem.Name.ToConflictFilePath(driveItem.FileSystemInfo.LastModifiedDateTime.Value);
            else
                name = driveItem.Name;

            var filePath = name.ToAbsolutePath(_localDrivePath);

            using (var stream = File.OpenWrite(filePath))
            {
                driveItem.Content.Seek(0, SeekOrigin.Begin);
                driveItem.Content.CopyTo(stream);
            }

            File.SetLastWriteTimeUtc(filePath, driveItem.FileSystemInfo.LastModifiedDateTime.Value.DateTime);
        }

        public void Dispose()
        {
            _loggerProviders.ForEach(loggerProvider => loggerProvider.Dispose());

            Directory.Delete(_localDrivePath, true);
            Directory.Delete(_remoteDrivePath, true);
        }
    }
}