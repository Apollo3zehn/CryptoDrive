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
        private string _localDrivePath;
        private string _remoteDrivePath;
        private RemoteTestDrive _remoteDrive;
        private List<ILoggerProvider> _loggerProviders;
        private IOneDriveClient _oneDriveClient;
        private ILogger<OneDriveSynchronizer> _logger;

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

                // Act
                await synchronizer.SynchronizeTheUniverse();

                _logger.LogInformation("Test finished.");

                // Assert

            }
        }

        private void PrepareDrives()
        {
            var driveItemPool = new Dictionary<string, DriveItem>()
            {
                ["a1"] = this.CreateDriveItem("a", 1),
                ["a2"] = this.CreateDriveItem("a", 2),

                ["b1"] = this.CreateDriveItem("b", 1),

                ["c1"] = this.CreateDriveItem("c", 1),

                ["d1"] = this.CreateDriveItem("d", 1),

                ["e1"] = this.CreateDriveItem("e", 1),
                ["e1c"] = this.CreateDriveItem("e", 1, isConflicted: true),
                ["e2"] = this.CreateDriveItem("e", 2),

                ["f1c"] = this.CreateDriveItem("f", 1, isConflicted: true),
                ["f2"] = this.CreateDriveItem("f", 2),

                ["g1"] = this.CreateDriveItem("g", 1),
                ["g1c"] = this.CreateDriveItem("g", 1, isConflicted: true),

                ["h1"] = this.CreateDriveItem("h", 1),
                ["h1c"] = this.CreateDriveItem("h", 1, isConflicted: true),

                ["i1c"] = this.CreateDriveItem("i", 1, isConflicted: true),
                ["i2"] = this.CreateDriveItem("i", 2),

                ["j1c"] = this.CreateDriveItem("j", 1, isConflicted: true),
                ["j2"] = this.CreateDriveItem("j", 2),
                ["j3"] = this.CreateDriveItem("j", 3),

                ["k1"] = this.CreateDriveItem("k", 1),
                ["k1c"] = this.CreateDriveItem("k", 1, isConflicted: true),
            };

            // _driveItemLocal
            this.CreateLocalFile(driveItemPool["a1"]);

            this.CreateLocalFile(driveItemPool["b1"]);

            this.CreateLocalFile(driveItemPool["d1"]);

            this.CreateLocalFile(driveItemPool["e1c"]);
            this.CreateLocalFile(driveItemPool["e2"]);

            this.CreateLocalFile(driveItemPool["f1c"]);
            this.CreateLocalFile(driveItemPool["f2"]);

            this.CreateLocalFile(driveItemPool["g1"]);
            this.CreateLocalFile(driveItemPool["g1c"]);

            this.CreateLocalFile(driveItemPool["h1c"]);

            this.CreateLocalFile(driveItemPool["i1c"]);

            this.CreateLocalFile(driveItemPool["j1c"]);
            this.CreateLocalFile(driveItemPool["j2"]);

            this.CreateLocalFile(driveItemPool["k1"]);
            this.CreateLocalFile(driveItemPool["k1c"]);

            // _remoteDrive
            _remoteDrive = new RemoteTestDrive(_remoteDrivePath);

            _remoteDrive.Upload(driveItemPool["a2"]);
            _remoteDrive.Upload(driveItemPool["b1"]);
            _remoteDrive.Upload(driveItemPool["c1"]);
            _remoteDrive.Upload(driveItemPool["e1"]);
            _remoteDrive.Upload(driveItemPool["g1"]);
            _remoteDrive.Upload(driveItemPool["h1"]);
            _remoteDrive.Upload(driveItemPool["i2"]);
            _remoteDrive.Upload(driveItemPool["j3"]);
        }

        private DriveItem CreateDriveItem(string name, int version, bool isConflicted = false)
        {
            var lastModified = new DateTime(2019, 01, 01, version, 00, 00, DateTimeKind.Utc);

            if (isConflicted)
                name = name.ToConflictFilePath(lastModified);

            return new DriveItem
            {
                Name = name,
                Content = $"v{version}".ToMemorySteam(),
                LastModifiedDateTime = lastModified,
                ParentReference = new ItemReference() { Path = CryptoDriveConstants.PathPrefix }
            };
        }

        private void CreateLocalFile(DriveItem driveItem)
        {
            var filePath = driveItem.Name.ToAbsolutePath(_localDrivePath);

            using (var stream = File.OpenWrite(filePath))
            {
                stream.Seek(0, SeekOrigin.Begin);
                driveItem.Content.CopyTo(stream);
            }

            File.SetLastWriteTimeUtc(filePath, driveItem.LastModifiedDateTime.Value.DateTime);
        }

        public void Dispose()
        {
            _loggerProviders.ForEach(loggerProvider => loggerProvider.Dispose());

            Directory.Delete(_localDrivePath, true);
            Directory.Delete(_remoteDrivePath, true);
        }
    }
}