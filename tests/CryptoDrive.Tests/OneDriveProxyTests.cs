using CryptoDrive.Extensions;
using CryptoDrive.Graph;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
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
            var proxy = new OneDriveProxy(graphService.GraphClient, _logger, BatchRequestContentPatch.ApplyPatch);

            var tempPath = Path.GetTempPath();
            var filePath = Path.Combine(tempPath, $"small.txt");
            var content = DateTime.Now.ToString();
            File.WriteAllText(filePath, content);
            var fileInfo = new FileInfo(filePath);

            var driveItem = fileInfo.ToDriveItem(tempPath.TrimEnd('\\'));

            using var stream = File.OpenRead(filePath);
            driveItem.Content = stream;

            // Act
            if (!graphService.IsSignedIn)
                await graphService.SignInAsync();

            await proxy.CreateOrUpdateAsync(driveItem);

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
            var proxy = new OneDriveProxy(graphService.GraphClient, _logger, BatchRequestContentPatch.ApplyPatch);

            var tempPath = Path.GetTempPath();
            var filePath = Path.Combine(tempPath, $"large.txt");

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                fileStream.SetLength(4 * 1024 * 1024 + 1);
            }

            var fileInfo = new FileInfo(filePath);
            var driveItem = fileInfo.ToDriveItem(tempPath.TrimEnd('\\'));

            using var stream = File.OpenRead(filePath);
            driveItem.Content = stream;

            // Act
            if (!graphService.IsSignedIn)
                await graphService.SignInAsync();

            await proxy.CreateOrUpdateAsync(driveItem);

            // Assert
        }

        public void Dispose()
        {
            _loggerProviders.ForEach(loggerProvider => loggerProvider.Dispose());
        }
    }
}