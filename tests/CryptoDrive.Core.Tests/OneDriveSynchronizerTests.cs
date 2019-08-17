using CryptoDrive.Core;
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
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace OneDas.Core.Tests
{
    public class OneDriveSynchronizerTests
    {
        private string _rootFolderPath;
        private InMemoryDrive _remoteDrive;
        private List<DriveItem> _driveItemPool;
        private IOneDriveClient _oneDriveClient;
        private ILogger<OneDriveSynchronizer> _logger;
        private ITestOutputHelper _testOutputHelper;

        public OneDriveSynchronizerTests()
        {
            // directory
            _rootFolderPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName().Replace(".", string.Empty));
            Directory.CreateDirectory(_rootFolderPath);
            Directory.SetCurrentDirectory(_rootFolderPath);

            // logger
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddLogging(loggingBuilder => loggingBuilder
                .AddDebug()
                .AddProvider(new XunitLoggerProvider(_testOutputHelper))
            );

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
                .Returns<string, string>((localFilePath, remoteFilePath) =>
            {
                using (var stream = File.OpenRead(localFilePath))
                {
                    return Task.FromResult(_remoteDrive.Upload(remoteFilePath, stream, File.GetLastWriteTimeUtc(localFilePath)));
                }                
            });

            _oneDriveClient = oneDriveClientMock.Object;
        }

        [Fact]
        public async void SynchronizeTheUniverseTest()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CryptoDriveDbContext>()
                .UseInMemoryDatabase(databaseName: "CryptoDrive")
                .Options;

            this.PrepareDrives();

            using (var context = new CryptoDriveDbContext(options))
            {
                var synchronizer = new OneDriveSynchronizer(_rootFolderPath, _oneDriveClient, context, _logger);

                // Act
                await synchronizer.SynchronizeTheUniverse();

                // Assert
                   
            }
        }

        private void PrepareDrives()
        {
            // _driveItemPool
            _driveItemPool = new List<DriveItem>()
            {
                new DriveItem { Name = "a", Content = "v1".ToMemorySteam(), LastModifiedDateTime = new DateTime(2019, 01, 01, 15, 00, 00), ParentReference = new ItemReference() { Path = CryptoDriveConstants.PathPrefix } },
                new DriveItem { Name = "a", Content = "v2".ToMemorySteam(), LastModifiedDateTime = new DateTime(2019, 01, 01, 15, 01, 00), ParentReference = new ItemReference() { Path = CryptoDriveConstants.PathPrefix } },
                new DriveItem { Name = "b", Content = "v1".ToMemorySteam(), LastModifiedDateTime = new DateTime(2019, 01, 01, 15, 00, 00), ParentReference = new ItemReference() { Path = CryptoDriveConstants.PathPrefix } },
                new DriveItem { Name = "c", Content = "v1".ToMemorySteam(), LastModifiedDateTime = new DateTime(2019, 01, 01, 15, 00, 00), ParentReference = new ItemReference() { Path = CryptoDriveConstants.PathPrefix } }
            };

            // _driveItemLocal
            this.CreateLocalFile(_driveItemPool[0]);
            this.CreateLocalFile(_driveItemPool[2]);

            // _remoteDrive
            _remoteDrive = new InMemoryDrive();
            _remoteDrive.Upload(_driveItemPool[1]);
            //_remoteDrive.Upload(_driveItemLightPool[2]);
            //_remoteDrive.Upload(_driveItemLightPool[3]);
        }

        private void CreateLocalFile(DriveItem driveItem)
        {
            using (var stream = File.OpenWrite(driveItem.Name))
            {
                driveItem.Content.CopyTo(stream);
            }

            File.SetLastWriteTimeUtc(driveItem.Name, driveItem.LastModifiedDateTime.Value.DateTime);
        }
    }
}