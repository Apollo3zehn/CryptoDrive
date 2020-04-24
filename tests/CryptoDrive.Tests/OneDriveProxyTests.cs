using CryptoDrive.Drives;
using CryptoDrive.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace CryptoDrive.Core.Tests
{
    public class OneDriveProxyTests : IDisposable
    {
        private List<ILoggerProvider> _loggerProviders;
        private ILogger<CryptoDriveSyncEngine> _logger;

        public OneDriveProxyTests(ITestOutputHelper xunitLogger)
        {
            (_logger, _loggerProviders) = Utils.GetLogger(xunitLogger);
        }

        [Fact(Skip = "Only for manual execution.")]
        public async void CanUploadSmallFileTest()
        {
            // Arrange
            var graphOptions = new GraphOptions()
            {
                ClientId = "7e3149de-a06d-4050-9e70-f2ebc84f3a76",
                RedirectUrl = "http://localhost:44959/oauth2/nativeclient",
                Scopes = "Files.ReadWrite.All User.Read"
            };

            var options = Options.Create(graphOptions);
            var graphService = new GraphService(options);
            var drive = await OneDriveProxy.CreateAsync(graphService.GraphClient,
                                                        graphService.GetAccountType(),
                                                        _logger,
                                                        BatchRequestContentPatch.ApplyPatch);

            var tempPath = Path.GetTempPath();
            var filePath = Path.Combine(tempPath, $"small.txt");
            var stringContent = DateTime.Now.ToString();
            File.WriteAllText(filePath, stringContent);
            var fileInfo = new FileInfo(filePath);

            var driveItem = fileInfo.ToDriveItem(tempPath.TrimEnd('\\'));

            using var content = File.OpenRead(filePath);

            // Act
            if (!graphService.IsSignedIn)
                await graphService.SignInAsync();

            await drive.CreateOrUpdateAsync(driveItem, content, CancellationToken.None);

            // Assert
        }

        [Fact(Skip = "Only for manual execution.")]
        public async void CanUploadLargeFileTest()
        {
            // Arrange
            var graphOptions = new GraphOptions()
            {
                ClientId = "7e3149de-a06d-4050-9e70-f2ebc84f3a76",
                RedirectUrl = "http://localhost:44959/oauth2/nativeclient",
                Scopes = "Files.ReadWrite.All User.Read"
            };

            var options = Options.Create(graphOptions);
            var graphService = new GraphService(options);
            var drive = await OneDriveProxy.CreateAsync(graphService.GraphClient,
                                                        graphService.GetAccountType(),
                                                        _logger,
                                                        BatchRequestContentPatch.ApplyPatch);

            var tempPath = Path.GetTempPath();
            var filePath = Path.Combine(tempPath, $"large.txt");

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                fileStream.SetLength(4 * 1024 * 1024 + 1);
            }

            var fileInfo = new FileInfo(filePath);
            var driveItem = fileInfo.ToDriveItem(tempPath.TrimEnd('\\'));

            using var content = File.OpenRead(filePath);

            // Act
            if (!graphService.IsSignedIn)
                await graphService.SignInAsync();

            await drive.CreateOrUpdateAsync(driveItem, content, CancellationToken.None);

            // Assert
        }

        public void Dispose()
        {
            _loggerProviders.ForEach(loggerProvider => loggerProvider.Dispose());
        }
    }
}