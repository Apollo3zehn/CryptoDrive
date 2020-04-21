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
        public CryptoDriveLocation DriveLocation { get; set; }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var path = value as string;

            switch (this.DriveLocation)
            {
                case CryptoDriveLocation.Local:

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        if (!Regex.IsMatch(path, @"[a-zA-Z]:(\\[^\\]*)*")
                         && !Regex.IsMatch(path, @"[a-zA-Z]:(\/[^\/]*)*"))
                            return new ValidationResult("The path is invalid.");
                    }
                    else
                    {
                        if (!Regex.IsMatch(path, @"\/(\/[^\/|\s|\\]*)*"))
                            return new ValidationResult("The path is invalid.");
                    }

                    if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                        return new ValidationResult("The path contains invalid characters.");

                    break;

                case CryptoDriveLocation.Remote:
                    if (!Regex.IsMatch(path, @"\/(\/[^\/|\s|\\]*)*"))
                        return new ValidationResult("The path is invalid.");

                    if (path.IndexOfAny(new char[] { '\0', ' ' }) >= 0)
                        return new ValidationResult("The path contains invalid characters.");

                    break;

                default:
                    throw new NotSupportedException();
            }

            return ValidationResult.Success;
        }
    }
}
