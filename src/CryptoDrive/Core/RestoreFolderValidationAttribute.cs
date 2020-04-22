using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace CryptoDrive.Core
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class RestoreFolderValidationAttribute : ValidationAttribute
    {
        #region Methods

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var path = value as string;
            var memberNames = new[] { validationContext.MemberName };

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

            if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
                return new ValidationResult("The directory is not empty.", memberNames);

            return ValidationResult.Success;
        }

        #endregion
    }
}
