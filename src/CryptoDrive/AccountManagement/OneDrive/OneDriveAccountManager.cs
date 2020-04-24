using CryptoDrive.Core;
using CryptoDrive.Drives;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace CryptoDrive.AccountManagement
{
    public class OneDriveAccountManager : IOneDriveService
    {
        #region Fields

        private string[] _scopes;

        private IPublicClientApplication _app;
        private IWebWindowManager _webWindowManager;

        private OneDriveOptions _options;

        #endregion

        #region Constructors

        public OneDriveAccountManager(IOptions<OneDriveOptions> options, IWebWindowManager webWindowManager = null)
        {
            _options = options.Value;
            _webWindowManager = webWindowManager;
            _scopes = _options.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            _app = PublicClientApplicationBuilder.Create(_options.ClientId)
                .WithRedirectUri(_options.RedirectUrl)
                .Build();

            TokenCacheHelper.EnableSerialization(_app.UserTokenCache);
        }

        #endregion

        #region Methods

        public async Task<IGraphServiceClient> CreateGraphClientAsync(string username)
        {
            var account = await this.GetAccountAsync(username);

            var authProvider = new DelegateAuthenticationProvider(
                async requestMessage =>
                {
                    var accessToken = (await _app.AcquireTokenSilent(_scopes, account).ExecuteAsync()).AccessToken;
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                });

            return new GraphServiceClient(authProvider);
        }

        public async Task<OneDriveAccountType> GetAccountTypeAsync(string username)
        {
            var account = await this.GetAccountAsync(username);

            return account.HomeAccountId.TenantId == OneDriveConstants.PersonalAccountTenantId
                    ? OneDriveAccountType.Personal
                    : OneDriveAccountType.WorkOrSchool;
        }

        public async Task<string> SignInAsync()
        {
            SystemWebViewOptions webViewOptions;

            if (_webWindowManager == null)
                webViewOptions = new SystemWebViewOptions();
            else
                webViewOptions = new SystemWebViewOptions()
                {
                    BrowserRedirectSuccess = new Uri(Program.BaseUrl),
                    BrowserRedirectError = new Uri(Program.BaseUrl),
                    OpenBrowserAsync = uri => this.NavigateToAsync(uri)
                };

            var result = await _app
                .AcquireTokenInteractive(_scopes)
                .WithSystemWebViewOptions(webViewOptions)
                .ExecuteAsync();

            return result.Account.Username;

            // delete not yet working:
            // https://docs.microsoft.com/en-us/graph/api/application-delete?view=graph-rest-1.0&tabs=http
        }

        public async Task SignOutAsync(string username)
        {
            var account = (await _app.GetAccountsAsync()).First(account => account.Username == username);
            await _app.RemoveAsync(account);
        }

        private async Task<IAccount> GetAccountAsync(string username)
        {
            var accounts = await _app.GetAccountsAsync();
            var account = accounts.First(account => account.Username == username);

            return account;
        }

        private Task NavigateToAsync(Uri uri)
        {
            _webWindowManager.NavigateToUrl(uri.ToString());

            return Task.CompletedTask;
        }

        #endregion
    }

    public interface IOneDriveService : IAccountManager
    {
        #region Methods

        Task<IGraphServiceClient> CreateGraphClientAsync(string username);

        Task<OneDriveAccountType> GetAccountTypeAsync(string username);

        #endregion
    }
}
