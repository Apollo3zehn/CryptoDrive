using System;
using System.ComponentModel.DataAnnotations;

namespace CryptoDrive.Core
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class RestoreKeyValidationAttribute : ValidationAttribute
    {
        #region Methods

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var key = value as string;
            var memberNames = new[] { validationContext.MemberName };

            try
            {
                var rawKey = Convert.FromBase64String(key);

                return (rawKey.Length * 8) == CryptoDriveConfiguration.KeySize
                    ? ValidationResult.Success
                    : new ValidationResult($"The key size must be {CryptoDriveConfiguration.KeySize} bits.", memberNames);
            }
            catch
            {
                return new ValidationResult($"The key must be a valid base64 string.", memberNames);
            }
        }

        #endregion
    }
}
