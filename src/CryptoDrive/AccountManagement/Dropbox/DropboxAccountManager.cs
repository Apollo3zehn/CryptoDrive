using System;
using System.Threading.Tasks;

namespace CryptoDrive.AccountManagement
{
    public class DropboxAccountManager : IDropboxAccountManager
    {
        public Task<string> SignInAsync()
        {
            throw new NotImplementedException();
        }

        public Task SignOutAsync(string username)
        {
            throw new NotImplementedException();
        }
    }

    public interface IDropboxAccountManager : IAccountManager
    {

    }
}
