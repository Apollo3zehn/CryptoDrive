using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CryptoDrive.Core.Tests
{
    public class CoreUtilitiesTests
    {
        [Fact]
        public void CanMergeChanges1()
        {
            // Arrange
            var changes = new Dictionary<string, DriveChangedType>();
            changes["/a/b/c"] = DriveChangedType.Self;
            changes["/a/b"] = DriveChangedType.Descendants;
            changes["/a"] = DriveChangedType.Self;
            changes["/a/b/c/d"] = DriveChangedType.Self;

            // Act
            var actual = CoreUtilities.MergeChanges(changes);

            // Assert
            var expected = new List<DriveChangedNotification>()
            {
                new DriveChangedNotification("/a/b", DriveChangedType.Descendants),
                new DriveChangedNotification("/a", DriveChangedType.Self),
            };

            Assert.True(expected.SequenceEqual(actual));
        }

        [Fact]
        public void CanMergeChanges2()
        {
            // Arrange
            var changes = new Dictionary<string, DriveChangedType>();
            changes["/b"] = DriveChangedType.Descendants;
            changes["/b/b2"] = DriveChangedType.Descendants;
            changes["/a/b/c/d/e/f"] = DriveChangedType.Descendants;
            changes["/a/b"] = DriveChangedType.Self;
            changes["/a/b/c"] = DriveChangedType.Descendants;
            changes["/a"] = DriveChangedType.Self;

            // Act
            var actual = CoreUtilities.MergeChanges(changes);

            // Assert
            var expected = new List<DriveChangedNotification>()
            {
                new DriveChangedNotification("/b", DriveChangedType.Descendants),
                new DriveChangedNotification("/a/b", DriveChangedType.Self),
                new DriveChangedNotification("/a/b/c", DriveChangedType.Descendants),
                new DriveChangedNotification("/a", DriveChangedType.Self),
            };

            Assert.True(expected.SequenceEqual(actual));
        }
    }
}
