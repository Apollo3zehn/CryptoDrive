using System.Threading.Tasks;

namespace CryptoDrive.AccountManagement
{
    public interface IAccountManager
    {
        #region Methods

        Task<string> SignInAsync();

        Task SignOutAsync(string username);

        #endregion
    }
}
