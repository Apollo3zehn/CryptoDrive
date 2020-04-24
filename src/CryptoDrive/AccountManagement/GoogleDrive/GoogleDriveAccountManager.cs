using System;
using System.Threading.Tasks;

namespace CryptoDrive.AccountManagement
{
    public class GoogleDriveAccountManager : IGoogleDriveAccountManager
    {
        public Task CreateGoogleDriveClientAsync(string username)
        {
            throw new NotImplementedException();
        }

        public Task<string> SignInAsync()
        {
            throw new NotImplementedException();
        }

        public Task SignOutAsync(string username)
        {
            throw new NotImplementedException();
        }
    }

    public interface IGoogleDriveAccountManager : IAccountManager
    {
        Task CreateGoogleDriveClientAsync(string username);
    }
}
