using CryptoDrive.Helpers;
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
        private TestDrive _remoteDrive;
        private List<DriveItemLight> _driveItemLightPool;
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

            // _driveItemPool
            _driveItemLightPool = new List<DriveItemLight>()
            {
                new DriveItemLight { Name = "a", Content = "v1", LastModifiedDateTime = new DateTime(2019, 01, 01, 15, 00, 00) },
                new DriveItemLight { Name = "a", Content = "v2", LastModifiedDateTime = new DateTime(2019, 01, 01, 15, 01, 00) },
                new DriveItemLight { Name = "b", Content = "v1", LastModifiedDateTime = new DateTime(2019, 01, 01, 15, 00, 00) },
                new DriveItemLight { Name = "c", Content = "v1", LastModifiedDateTime = new DateTime(2019, 01, 01, 15, 00, 00) },
            };

            // _driveItemLocal
            this.CreateLocalFile(_driveItemLightPool[0]);
            this.CreateLocalFile(_driveItemLightPool[2]);

            // _remoteDrive
            _remoteDrive = new TestDrive();
            _remoteDrive.Upload(_driveItemLightPool[1]);
            _remoteDrive.Upload(_driveItemLightPool[2]);
            _remoteDrive.Upload(_driveItemLightPool[3]);

            // _graphClient
            var oneDriveClientMock = new Mock<IOneDriveClient>();

            oneDriveClientMock
                .Setup(x => x.GetDeltaAsync(It.IsAny<string>()))
                .ReturnsAsync(() => _remoteDrive.GetDelta());

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

            using (var context = new CryptoDriveDbContext(options))
            {
                var synchronizer = new OneDriveSynchronizer(_rootFolderPath, _oneDriveClient, context, _logger);

                // Act
                await synchronizer.SynchronizeTheUniverse();

                // Assert
                
            }
        }
        private void CreateLocalFile(DriveItemLight driveItemLight)
        {
            using (var stream = File.OpenWrite(driveItemLight.Name))
            {
                driveItemLight.ContentStream.CopyTo(stream);
            }
        }
    }
}