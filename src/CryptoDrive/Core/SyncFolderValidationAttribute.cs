using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace CryptoDrive.Core
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SyncFolderValidationAttribute : ValidationAttribute
    {
        #region Constructors

        public SyncFolderValidationAttribute(CryptoDriveLocation driveLocation)
        {
            this.DriveLocation = driveLocation;
        }

        #endregion

        #region Properties

        public CryptoDriveLocation DriveLocation { get; }

        #endregion

        #region Methods

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var path = value as string;
            var memberNames = new[] { validationContext.MemberName };

            switch (this.DriveLocation)
            {
                case CryptoDriveLocation.Local:

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        if (!Regex.IsMatch(path, @"^[a-zA-Z]:(\\[^\\]*)*")
                         && !Regex.IsMatch(path, @"^[a-zA-Z]:(\/[^\/]*)*"))
                            return new ValidationResult("The path is invalid.", memberNames);
                    }
                    else
                    {
                        if (!Regex.IsMatch(path, @"^\/(\/*[^\/|\s|\\]*)*"))
                            return new ValidationResult("The path is invalid.", memberNames);
                    }

                    if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                        return new ValidationResult("The path contains invalid characters.", memberNames);

                    break;

                case CryptoDriveLocation.Remote:
                    if (!Regex.IsMatch(path, @"^\/(\/*[^\/|\s|\\]*)*"))
                        return new ValidationResult("The path is invalid.", memberNames);

                    if (path.IndexOfAny(new char[] { '\0', ' ' }) >= 0)
                        return new ValidationResult("The path contains invalid characters.", memberNames);

                    break;

                default:
                    throw new NotSupportedException();
            }

            return ValidationResult.Success;
        }

        #endregion
    }
}
