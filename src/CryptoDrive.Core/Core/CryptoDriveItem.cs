using CryptoDrive.Extensions;
using System;

namespace CryptoDrive.Core
{
    public class CryptoDriveItem
    {
        #region Constructors

        public CryptoDriveItem(string name, string path, DriveItemType type)
            : this(string.Empty, name, path, type)
        {
            //
        }

        public CryptoDriveItem(string id, string name, string path, DriveItemType type)
        {
            this.Id = id;
            this.Name = name;
            this.Path = path;
            this.Type = type;
        }

        #endregion

        #region Properties

        public bool IsDeleted { get; set; }

        public DriveItemType Type { get; }

        public DateTime LastModified { get; set; }

        public string Id { get; set; }

        public string Name { get; set; }

        public string Path { get; set; }

        public long Size { get; set; }

        #endregion

        #region Methods

        public string GetItemPath()
        {
            return Utilities.PathCombine(this.Path, this.Name);
        }

        public string GetAbsolutePath(string basePath)
        {
            return this.GetItemPath().ToAbsolutePath(basePath);
        }

        public new CryptoDriveItem MemberwiseClone()
        {
            return new CryptoDriveItem(this.Id, this.Name, this.Path, this.Type)
            {
                IsDeleted = this.IsDeleted,
                LastModified = this.LastModified,
                Size = this.Size
            };
        }

        #endregion
    }
}
