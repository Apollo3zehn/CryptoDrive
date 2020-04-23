using CryptoDrive.Core;
using CryptoDrive.Drives;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace CryptoDrive.Graph
{
    public class GraphService : IGraphService
    {
        #region Fields

        private string[] _scopes;
        private GraphOptions _options;

        private IAccount _account;
        private IPublicClientApplication _app;
        private IWebWindowManager _webWindowManager;

        #endregion

        #region Constructors

        public GraphService(IOptions<GraphOptions> options, IWebWindowManager webWindowManager = null)
        {
            _options = options.Value;
            _webWindowManager = webWindowManager;
            _scopes = _options.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            _app = PublicClientApplicationBuilder.Create(_options.ClientId)
                .WithRedirectUri(_options.RedirectUrl)
                .Build();

            TokenCacheHelper.EnableSerialization(_app.UserTokenCache);

            _account = _app.GetAccountsAsync().Result.FirstOrDefault();

            var authProvider = new DelegateAuthenticationProvider(
                async requestMessage =>
                {
                    if (_account == null)
                        throw new Exception("The user must be signed in before any requests to the graph API can be issued.");

                    var accessToken = (await _app.AcquireTokenSilent(_scopes, _account).ExecuteAsync()).AccessToken;
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                });

            this.GraphClient = new GraphServiceClient(authProvider);
        }

        #endregion

        #region Properties

        public IGraphServiceClient GraphClient { get; }

        public bool IsSignedIn => _account != null;

        #endregion

        #region Methods

        public OneDriveAccountType GetAccountType()
        {
            if (this.IsSignedIn)
                return _account.HomeAccountId.TenantId == OneDriveConstants.PersonalAccountTenantId
                    ? OneDriveAccountType.Personal
                    : OneDriveAccountType.WorkOrSchool;

            else
                throw new Exception("The user is not signed in.");
        }

        public async Task SignInAsync()
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

            await _app
                .AcquireTokenInteractive(_scopes)
                .WithSystemWebViewOptions(webViewOptions)
                .ExecuteAsync();

            _account = _app.GetAccountsAsync().Result.First();

            // delete not yet working:
            // https://docs.microsoft.com/en-us/graph/api/application-delete?view=graph-rest-1.0&tabs=http
        }

        public async Task SignOutAsync()
        {
            var accounts = await _app.GetAccountsAsync();

            foreach (var account in accounts)
            {
                await _app.RemoveAsync(account);
            }

            _account = null;

            // How to remove the app in the online profile, too?
        }

        private Task NavigateToAsync(Uri uri)
        {
            _webWindowManager.NavigateToUrl(uri.ToString());

            return Task.CompletedTask;
        }

        #endregion
    }

    public interface IGraphService
    {
        #region Properties

        IGraphServiceClient GraphClient { get; }

        bool IsSignedIn { get; }

        #endregion

        #region Methods

        Task SignInAsync();

        Task SignOutAsync();

        OneDriveAccountType GetAccountType();

        #endregion
    }
}
