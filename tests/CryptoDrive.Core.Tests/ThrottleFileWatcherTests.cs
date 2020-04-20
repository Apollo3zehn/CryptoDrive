using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace CryptoDrive.Core.Tests
{
    public class ThrottleFileWatcherTests
    {
        [Fact]
        public void CanTrackFolderCreate()
        {
            // Arrange
            DriveChangedEventArgs actual = default;

            var folderPath = this.PrepareFolder();
            var fileWatcher = new ThrottleFileWatcher(folderPath);
            var manualResetEvent = new ManualResetEvent(initialState: false);

            fileWatcher.DriveChanged += (sender, e) =>
            {
                actual = e;
                manualResetEvent.Set();
            };

            fileWatcher.EnableRaisingEvents = true;

            // Act
            var itemPath = Path.Combine(folderPath, "a/b/b2");
            Directory.CreateDirectory(itemPath);

            manualResetEvent.WaitOne(timeout: TimeSpan.FromSeconds(30));

            // Assert
            var expected = new DriveChangedEventArgs(new List<DriveChangedNotification>()
            {
                new DriveChangedNotification("/a/b", DriveChangedType.Self),
                new DriveChangedNotification("/a/b/b2", DriveChangedType.Descendants)
            });

            Assert.True(expected.ChangeNotifications.SequenceEqual(actual.ChangeNotifications));
        }

        [Fact]
        public void CanTrackFolderDelete()
        {
            // Arrange
            DriveChangedEventArgs actual = default;

            var folderPath = this.PrepareFolder();
            var fileWatcher = new ThrottleFileWatcher(folderPath);
            var manualResetEvent = new ManualResetEvent(initialState: false);

            fileWatcher.DriveChanged += (sender, e) =>
            {
                actual = e;
                manualResetEvent.Set();
            };

            fileWatcher.EnableRaisingEvents = true;

            // Act
            var itemPath = Path.Combine(folderPath, "a");
            Directory.Delete(itemPath, recursive: true);

            manualResetEvent.WaitOne(timeout: TimeSpan.FromSeconds(30));

            // Assert
            var expected = new DriveChangedEventArgs(new List<DriveChangedNotification>()
            {
                new DriveChangedNotification("/a", DriveChangedType.Descendants),
                new DriveChangedNotification("/", DriveChangedType.Self)
            });

            Assert.True(expected.ChangeNotifications.SequenceEqual(actual.ChangeNotifications));
        }

        [Fact]
        public void CanTrackFolderMove1()
        {
            // Arrange
            DriveChangedEventArgs actual = default;

            var folderPathSource = this.PrepareFolder();
            var folderPathTarget = this.PrepareFolder();

            var fileWatcher = new ThrottleFileWatcher(folderPathSource);
            var manualResetEvent = new ManualResetEvent(initialState: false);

            fileWatcher.DriveChanged += (sender, e) =>
            {
                actual = e;
                manualResetEvent.Set();
            };

            fileWatcher.EnableRaisingEvents = true;

            // Act
            var itemPath = Path.Combine(folderPathSource, "a");
            Directory.Move(itemPath, Path.Combine(folderPathTarget, "a_new"));

            manualResetEvent.WaitOne(timeout: TimeSpan.FromSeconds(30));

            // Assert
            var expected = new DriveChangedEventArgs(new List<DriveChangedNotification>()
            {
                new DriveChangedNotification("/", DriveChangedType.Self),
                new DriveChangedNotification("/a", DriveChangedType.Descendants)
            });

            Assert.True(expected.ChangeNotifications.SequenceEqual(actual.ChangeNotifications));
        }

        [Fact]
        public void CanTrackFolderMove2()
        {
            // Arrange
            DriveChangedEventArgs actual = default;

            var folderPathSource = this.PrepareFolder();
            var folderPathTarget = this.PrepareFolder();

            var fileWatcher = new ThrottleFileWatcher(folderPathTarget);
            var manualResetEvent = new ManualResetEvent(initialState: false);

            fileWatcher.DriveChanged += (sender, e) =>
            {
                actual = e;
                manualResetEvent.Set();
            };

            fileWatcher.EnableRaisingEvents = true;

            // Act
            var itemPath = Path.Combine(folderPathTarget, "a_new");
            Directory.Move(Path.Combine(folderPathSource, "a"), itemPath);

            manualResetEvent.WaitOne(timeout: TimeSpan.FromSeconds(30));

            // Assert
            var expected = new DriveChangedEventArgs(new List<DriveChangedNotification>()
            {
                new DriveChangedNotification("/", DriveChangedType.Self),
                new DriveChangedNotification("/a_new", DriveChangedType.Descendants)
            });

            Assert.True(expected.ChangeNotifications.SequenceEqual(actual.ChangeNotifications));
        }

        [Fact]
        public void CanTrackFolderMove3()
        {
            // Arrange
            DriveChangedEventArgs actual = default;

            var folderPath = this.PrepareFolder();
            var fileWatcher = new ThrottleFileWatcher(folderPath);
            var manualResetEvent = new ManualResetEvent(initialState: false);

            fileWatcher.DriveChanged += (sender, e) =>
            {
                actual = e;
                manualResetEvent.Set();
            };

            fileWatcher.EnableRaisingEvents = true;

            // Act
            var sourceFolderPath = Path.Combine(folderPath, "b/f");
            var targetFolderPath = Path.Combine(folderPath, "a/b/c/f");
            Directory.Move(sourceFolderPath, targetFolderPath);

            manualResetEvent.WaitOne(timeout: TimeSpan.FromSeconds(30));

            // Assert
            var expected = new DriveChangedEventArgs(new List<DriveChangedNotification>()
            {
                new DriveChangedNotification("/b", DriveChangedType.Self),
                new DriveChangedNotification("/b/f", DriveChangedType.Descendants),
                new DriveChangedNotification("/a/b/c", DriveChangedType.Self),
                new DriveChangedNotification("/a/b/c/f", DriveChangedType.Descendants),
            });

            Assert.True(expected.ChangeNotifications.SequenceEqual(actual.ChangeNotifications));
        }

        [Fact]
        public void CanTrackFolderMove4()
        {
            // Arrange
            DriveChangedEventArgs actual = default;

            var folderPath = this.PrepareFolder();
            var fileWatcher = new ThrottleFileWatcher(folderPath);
            var manualResetEvent = new ManualResetEvent(initialState: false);

            fileWatcher.DriveChanged += (sender, e) =>
            {
                actual = e;
                manualResetEvent.Set();
            };

            fileWatcher.EnableRaisingEvents = true;

            // Act
            var sourceFolderPath = Path.Combine(folderPath, "b/f");
            var targetFolderPath = Path.Combine(folderPath, "b/f_new");
            Directory.Move(sourceFolderPath, targetFolderPath);

            manualResetEvent.WaitOne(timeout: TimeSpan.FromSeconds(30));

            // Assert
            var expected = new DriveChangedEventArgs(new List<DriveChangedNotification>()
            {
                new DriveChangedNotification("/b", DriveChangedType.Self),
                new DriveChangedNotification("/b/f_new", DriveChangedType.Descendants),
                new DriveChangedNotification("/b/f", DriveChangedType.Descendants)
            });

            Assert.True(expected.ChangeNotifications.SequenceEqual(actual.ChangeNotifications));
        }

        [Fact]
        public void CanTrackFileCreate()
        {
            // Arrange
            DriveChangedEventArgs actual = default;

            var folderPath = this.PrepareFolder();
            var fileWatcher = new ThrottleFileWatcher(folderPath);
            var manualResetEvent = new ManualResetEvent(initialState: false);

            fileWatcher.DriveChanged += (sender, e) =>
            {
                actual = e;
                manualResetEvent.Set();
            };

            fileWatcher.EnableRaisingEvents = true;

            // Act
            var itemPath = Path.Combine(folderPath, "a/Pikachu");
            using (File.Create(itemPath)) { };

            manualResetEvent.WaitOne(timeout: TimeSpan.FromSeconds(30));

            // Assert
            var expected = new DriveChangedEventArgs(new List<DriveChangedNotification>()
            {
                new DriveChangedNotification("/a", DriveChangedType.Self),
            });

            Assert.True(expected.ChangeNotifications.SequenceEqual(actual.ChangeNotifications));
        }

        [Fact]
        public void CanTrackFileDelete()
        {
            // Arrange
            DriveChangedEventArgs actual = default;

            var folderPath = this.PrepareFolder();
            var fileWatcher = new ThrottleFileWatcher(folderPath);
            var manualResetEvent = new ManualResetEvent(initialState: false);

            fileWatcher.DriveChanged += (sender, e) =>
            {
                actual = e;
                manualResetEvent.Set();
            };

            fileWatcher.EnableRaisingEvents = true;

            // Act
            var itemPath = Path.Combine(folderPath, "a/b/c", "Glumanda");
            File.Delete(itemPath);

            manualResetEvent.WaitOne(timeout: TimeSpan.FromSeconds(30));

            // Assert
            var expected = new DriveChangedEventArgs(new List<DriveChangedNotification>()
            {
                new DriveChangedNotification("/a/b/c", DriveChangedType.Self)
            });

            Assert.True(expected.ChangeNotifications.SequenceEqual(actual.ChangeNotifications));
        }

        [Fact]
        public void CanTrackFileMove1()
        {
            // Arrange
            DriveChangedEventArgs actual = default;

            var folderPathSource = this.PrepareFolder();
            var folderPathTarget = this.PrepareFolder();

            var fileWatcher = new ThrottleFileWatcher(folderPathSource);
            var manualResetEvent = new ManualResetEvent(initialState: false);

            fileWatcher.DriveChanged += (sender, e) =>
            {
                actual = e;
                manualResetEvent.Set();
            };

            fileWatcher.EnableRaisingEvents = true;

            // Act
            var itemPath = Path.Combine(folderPathSource, "a/b/c", "Safcon");
            File.Move(itemPath, Path.Combine(folderPathTarget, "a/b/c", "Safcon_new"));

            manualResetEvent.WaitOne(timeout: TimeSpan.FromSeconds(30));

            // Assert
            var expected = new DriveChangedEventArgs(new List<DriveChangedNotification>()
            {
                new DriveChangedNotification("/a/b/c", DriveChangedType.Self)
            });

            Assert.True(expected.ChangeNotifications.SequenceEqual(actual.ChangeNotifications));
        }

        [Fact]
        public void CanTrackFileMove2()
        {
            // Arrange
            DriveChangedEventArgs actual = default;

            var folderPathSource = this.PrepareFolder();
            var folderPathTarget = this.PrepareFolder();

            var fileWatcher = new ThrottleFileWatcher(folderPathTarget);
            var manualResetEvent = new ManualResetEvent(initialState: false);

            fileWatcher.DriveChanged += (sender, e) =>
            {
                actual = e;
                manualResetEvent.Set();
            };

            fileWatcher.EnableRaisingEvents = true;

            // Act
            var itemPath = Path.Combine(folderPathTarget, "a/b/c", "Safcon_new");
            File.Move(Path.Combine(folderPathSource, "a/b/c", "Safcon"), itemPath);

            manualResetEvent.WaitOne(timeout: TimeSpan.FromSeconds(30));

            // Assert
            var expected = new DriveChangedEventArgs(new List<DriveChangedNotification>()
            {
                new DriveChangedNotification("/a/b/c", DriveChangedType.Self)
            });

            Assert.True(expected.ChangeNotifications.SequenceEqual(actual.ChangeNotifications));
        }

        [Fact]
        public void CanTrackFileMove3()
        {
            // Arrange
            DriveChangedEventArgs actual = default;

            var folderPath = this.PrepareFolder();
            var fileWatcher = new ThrottleFileWatcher(folderPath);
            var manualResetEvent = new ManualResetEvent(initialState: false);

            fileWatcher.DriveChanged += (sender, e) =>
            {
                actual = e;
                manualResetEvent.Set();
            };

            fileWatcher.EnableRaisingEvents = true;

            // Act
            var sourceFilePath = Path.Combine(folderPath, "a/b/Smettbo");
            var targetFilePath = Path.Combine(folderPath, "b/Smettbo");
            Directory.Move(sourceFilePath, targetFilePath);

            manualResetEvent.WaitOne(timeout: TimeSpan.FromSeconds(30));

            // Assert
            var expected = new DriveChangedEventArgs(new List<DriveChangedNotification>()
            {
                new DriveChangedNotification("/a/b", DriveChangedType.Self),
                new DriveChangedNotification("/b", DriveChangedType.Self),
            });

            Assert.True(expected.ChangeNotifications.SequenceEqual(actual.ChangeNotifications));
        }

        [Fact]
        public void CanTrackFileMove4()
        {
            // Arrange
            DriveChangedEventArgs actual = default;

            var folderPath = this.PrepareFolder();
            var fileWatcher = new ThrottleFileWatcher(folderPath);
            var manualResetEvent = new ManualResetEvent(initialState: false);

            fileWatcher.DriveChanged += (sender, e) =>
            {
                actual = e;
                manualResetEvent.Set();
            };

            fileWatcher.EnableRaisingEvents = true;

            // Act
            var sourceFilePath = Path.Combine(folderPath, "a/b/Smettbo");
            var targetFilePath = Path.Combine(folderPath, "a/b/Smettbo_new");
            Directory.Move(sourceFilePath, targetFilePath);

            manualResetEvent.WaitOne(timeout: TimeSpan.FromSeconds(30));

            // Assert
            var expected = new DriveChangedEventArgs(new List<DriveChangedNotification>()
            {
                new DriveChangedNotification("/a/b", DriveChangedType.Self),
            });

            Assert.True(expected.ChangeNotifications.SequenceEqual(actual.ChangeNotifications));
        }

        [Fact]
        public void CanTrackEditFileSize()
        {
            // Arrange
            DriveChangedEventArgs actual = default;

            var folderPath = this.PrepareFolder();
            var fileWatcher = new ThrottleFileWatcher(folderPath);
            var manualResetEvent = new ManualResetEvent(initialState: false);

            fileWatcher.DriveChanged += (sender, e) =>
            {
                actual = e;
                manualResetEvent.Set();
            };

            fileWatcher.EnableRaisingEvents = true;

            // Act
            var itemPath = Path.Combine(folderPath, "a/b/c", "Glumanda");

            using (var streamWriter = new StreamWriter(File.Open(itemPath, FileMode.Open, FileAccess.Write)))
            {
                streamWriter.WriteLine("Gotta Catch 'Em All!");
            }

            manualResetEvent.WaitOne(timeout: TimeSpan.FromSeconds(30));

            // Assert
            var expected = new DriveChangedEventArgs(new List<DriveChangedNotification>()
            {
                new DriveChangedNotification("/a/b/c", DriveChangedType.Self)
            });

            Assert.True(expected.ChangeNotifications.SequenceEqual(actual.ChangeNotifications));
        }

        [Fact]
        public void CanTrackEditFileDate()
        {
            // Arrange
            DriveChangedEventArgs actual = default;

            var folderPath = this.PrepareFolder();
            var fileWatcher = new ThrottleFileWatcher(folderPath);
            var manualResetEvent = new ManualResetEvent(initialState: false);

            fileWatcher.DriveChanged += (sender, e) =>
            {
                actual = e;
                manualResetEvent.Set();
            };

            fileWatcher.EnableRaisingEvents = true;

            // Act
            var itemPath = Path.Combine(folderPath, "a/b/c", "Glumanda");
            File.SetLastWriteTimeUtc(itemPath, new DateTime(1996, 02, 27));

            manualResetEvent.WaitOne(timeout: TimeSpan.FromSeconds(30));

            // Assert
            var expected = new DriveChangedEventArgs(new List<DriveChangedNotification>()
            {
                new DriveChangedNotification("/a/b/c", DriveChangedType.Self)
            });

            Assert.True(expected.ChangeNotifications.SequenceEqual(actual.ChangeNotifications));
        }

        [Fact]
        public void CanTrackCombined()
        {
            // Arrange
            DriveChangedEventArgs actual = default;

            var folderPath = this.PrepareFolder();
            var fileWatcher = new ThrottleFileWatcher(folderPath);
            var manualResetEvent = new ManualResetEvent(initialState: false);

            fileWatcher.DriveChanged += (sender, e) =>
            {
                actual = e;
                manualResetEvent.Set();
            };

            fileWatcher.EnableRaisingEvents = true;

            // Act
            var itemPath = Path.Combine(folderPath, "a/b/b2");
            Directory.CreateDirectory(itemPath);

            itemPath = Path.Combine(folderPath, "a/Pikachu");
            using (File.Create(itemPath)) { };

            itemPath = Path.Combine(folderPath, "a/b/c", "Glumanda");
            File.Delete(itemPath);

            var sourceFilePath = Path.Combine(folderPath, "a/b/Smettbo");
            var targetFilePath = Path.Combine(folderPath, "b/Smettbo");
            Directory.Move(sourceFilePath, targetFilePath);

            var sourceFolderPath = Path.Combine(folderPath, "b/f");
            var targetFolderPath = Path.Combine(folderPath, "a/b/c/f");
            Directory.Move(sourceFolderPath, targetFolderPath);

            itemPath = Path.Combine(folderPath, "b");
            Directory.Delete(itemPath, recursive: true);

            manualResetEvent.WaitOne(timeout: TimeSpan.FromSeconds(30));

            // Assert
            var expected = new DriveChangedEventArgs(new List<DriveChangedNotification>()
            {
                new DriveChangedNotification("/a/b", DriveChangedType.Self),
                new DriveChangedNotification("/a/b/b2", DriveChangedType.Descendants),
                new DriveChangedNotification("/a", DriveChangedType.Self),
                new DriveChangedNotification("/a/b/c", DriveChangedType.Self),
                new DriveChangedNotification("/b", DriveChangedType.Descendants),
                new DriveChangedNotification("/a/b/c/f", DriveChangedType.Descendants),
                new DriveChangedNotification("/", DriveChangedType.Self)
            });

            Assert.True(expected.ChangeNotifications.SequenceEqual(actual.ChangeNotifications));
        }

        private string PrepareFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "CryptoDrive", $"Test_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(folderPath);

            var folder1 = Path.Combine(folderPath, "a/b/c");
            Directory.CreateDirectory(folder1);

            var folder2 = Path.Combine(folderPath, "a/d/e");
            Directory.CreateDirectory(folder2);

            var folder3 = Path.Combine(folderPath, "b/f");
            Directory.CreateDirectory(folder3);

            var file1 = Path.Combine(folder1, "Glumanda");
            using (File.Create(file1)) { };

            var file2 = Path.Combine(folder1, "Safcon");
            using (File.Create(file2)) { };

            var file3 = Path.Combine(folderPath, "a/b", "Smettbo");
            using (File.Create(file3)) { };

            var file4 = Path.Combine(folder3, "Mewtu");
            using (File.Create(file4)) { };

            return folderPath;
        }
    }
}
