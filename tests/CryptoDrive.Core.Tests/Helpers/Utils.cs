using CryptoDrive.Drives;
using CryptoDrive.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using File = System.IO.File;

namespace CryptoDrive.Core.Tests
{
    public static class Utils
    {
        static Utils()
        {
            Utils.DriveItemPool = Utils.CreateDriveItemPool();
        }

        public static void CompareFiles(string fileName, string versionName, string basePath)
        {
            var hashAlgorithm = new QuickXorHash();

            using (var contentActual = File.OpenRead(fileName.ToAbsolutePath(basePath)))
            {
                var actual = Convert.ToBase64String(hashAlgorithm.ComputeHash(contentActual));

                var contentExpected = Utils.DriveItemPool[versionName]().Content;
                var expected = Convert.ToBase64String(hashAlgorithm.ComputeHash(contentExpected));

                Assert.True(actual == expected, "The contents are not equal.");
            }
        }

        public static (ILogger<CryptoDriveSyncEngine>, List<ILoggerProvider>) GetLogger(ITestOutputHelper xunitLogger)
        {
            List<ILoggerProvider> loggerProviders = null;

            var loggerFactory = LoggerFactory.Create(logging =>
            {
                logging
                    .AddSeq()
                    .AddProvider(new XunitLoggerProvider(xunitLogger))
                    .SetMinimumLevel(LogLevel.Trace);

                loggerProviders = logging.Services
                    .Where(descriptor => typeof(ILoggerProvider).IsAssignableFrom(descriptor.ImplementationInstance?.GetType()))
                    .Select(descriptor => (ILoggerProvider)descriptor.ImplementationInstance)
                    .ToList();
            });

            var logger = loggerFactory.CreateLogger<CryptoDriveSyncEngine>();

            return (logger, loggerProviders);
        }

        public static Dictionary<string, Func<DriveItem>> DriveItemPool { get; }

        public static async Task<DriveHive> PrepareDrives(string fileId, ILogger logger)
        {
            var remoteDrivePath = Path.Combine(Path.GetTempPath(), "CryptoDriveRemote_" + Guid.NewGuid().ToString());
            var remoteDrive = new LocalDriveProxy(remoteDrivePath, "OneDrive", logger, TimeSpan.FromMilliseconds(500));
            remoteDrive.EnableChangeTracking = false;

            var localDrivePath = Path.Combine(Path.GetTempPath(), "CryptoDriveLocal_" + Guid.NewGuid().ToString());
            var localDrive = new LocalDriveProxy(localDrivePath, "local", logger, TimeSpan.FromMilliseconds(500));
            localDrive.EnableChangeTracking = false;

            switch (fileId)
            {
                case "/a":
                    await localDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/a1"]());
                    await remoteDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/a2"]());
                    break;

                case "/sub/a":
                    await localDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/sub/a1"]());
                    await remoteDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/sub/a1"]());
                    break;

                case "/b":
                    await localDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/b1"]());
                    await remoteDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/b1"]());
                    break;

                case "/c":
                    await remoteDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/c1"]());
                    break;

                case "/d":
                    await localDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/d1"]());
                    break;

                case "/e":
                    await localDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/e2"]());
                    await remoteDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/e1"]());
                    break;

                case "/f":
                    await localDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/f2"]());
                    break;

                case "/g":
                    await localDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/g1"]());
                    await remoteDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/g1"]());
                    break;

                case "/h":
                    await remoteDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/h1"]());
                    break;

                case "/i":
                    await remoteDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/i2"]());
                    break;

                case "/j":
                    await localDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/j2"]());
                    await remoteDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/j3"]());
                    break;

                case "/k":
                    await localDrive.CreateOrUpdateAsync(Utils.DriveItemPool["/k1"]());
                    break;

                default:
                    break;
            }

            remoteDrive.EnableChangeTracking = true;
            localDrive.EnableChangeTracking = true;

            return new DriveHive(remoteDrive, localDrive, remoteDrivePath, localDrivePath);
        }

        public static Dictionary<string, Func<DriveItem>> CreateDriveItemPool()
        {
            return new Dictionary<string, Func<DriveItem>>()
            {
                ["/a1"] = () => Utils.CreateDriveItem("/a", 1),
                ["/a2"] = () => Utils.CreateDriveItem("/a", 2),

                ["/sub/a1"] = () => Utils.CreateDriveItem("/sub/a", 1),

                ["/b1"] = () => Utils.CreateDriveItem("/b", 1),

                ["/c1"] = () => Utils.CreateDriveItem("/c", 1),

                ["/d1"] = () => Utils.CreateDriveItem("/d", 1),

                ["/e1"] = () => Utils.CreateDriveItem("/e", 1),
                ["/e2"] = () => Utils.CreateDriveItem("/e", 2),

                ["/f1"] = () => Utils.CreateDriveItem("/f", 1),
                ["/f2"] = () => Utils.CreateDriveItem("/f", 2),

                ["/g1"] = () => Utils.CreateDriveItem("/g", 1),

                ["/h1"] = () => Utils.CreateDriveItem("/h", 1),

                ["/i1"] = () => Utils.CreateDriveItem("/i", 1),
                ["/i2"] = () => Utils.CreateDriveItem("/i", 2),

                ["/j1"] = () => Utils.CreateDriveItem("/j", 1),
                ["/j2"] = () => Utils.CreateDriveItem("/j", 2),
                ["/j3"] = () => Utils.CreateDriveItem("/j", 3),

                ["/k1"] = () => Utils.CreateDriveItem("/k", 1),
            };
        }

        private static DriveItem CreateDriveItem(string itemPath, int version)
        {
            var name = Path.GetFileName(itemPath);
            var folderPath = $"{Path.GetDirectoryName(itemPath)}".NormalizeSlashes();
            var lastModified = new DateTime(2019, 01, 01, version, 00, 00, DateTimeKind.Utc);

            var content = $"{itemPath} v{version}".ToMemorySteam();
            content.Position = 0;

            return new DriveItem
            {
                File = new Microsoft.Graph.File(),
                Name = name,
                Content = content,
                FileSystemInfo = new Microsoft.Graph.FileSystemInfo { LastModifiedDateTime = lastModified },
                ParentReference = new ItemReference() { Path = folderPath }
            };
        }
    }
}
