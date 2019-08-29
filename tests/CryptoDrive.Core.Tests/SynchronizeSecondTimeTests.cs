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

namespace CryptoDrive.Core.Tests
{
    [TestCaseOrderer("CryptoDrive.Core.Tests.PriorityOrderer", "CryptoDrive.Core.Tests")]
    public class SynchronizeSecondTimeTests : IDisposable
    {
        private List<ILoggerProvider> _loggerProviders;
        private ILogger<CryptoDriveSyncEngine> _logger;

        private DriveHive _driveHive;

        public SynchronizeSecondTimeTests(ITestOutputHelper xunitLogger)
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
        public async void CanSynchronizeSecondTime()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<CryptoDriveDbContext>()
                // InMemoryDatabase is currently broken
                //.UseInMemoryDatabase(databaseName: "CryptoDrive")
                .UseSqlite($"Data Source={Path.GetTempFileName()}")
                .Options;

            _driveHive = await Utils.PrepareDrives("a", _logger);

            using (var context = new CryptoDriveDbContext(options))
            {
                context.Database.EnsureCreated();

                var synchronizer = new CryptoDriveSyncEngine(_driveHive.RemoteDrive, _driveHive.LocalDrive, context, _logger);

                await synchronizer.Synchronize();
                await _driveHive.LocalDrive.DeleteAsync("a".ToConflictFilePath(new DateTime(2019, 01, 01, 02, 00, 00)).ToDriveItem());

                // Act

                //await synchronizer.Synchronize();

                // Assert
            }
        }

        public void Dispose()
        {
            _loggerProviders.ForEach(loggerProvider => loggerProvider.Dispose());

            Directory.Delete(_driveHive.RemoteDrivePath, true);
            Directory.Delete(_driveHive.LocalDrivePath, true);
        }
    }
}