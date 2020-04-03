using CryptoDrive.Core;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoDrive.Graph
{
    public class GraphService : IGraphService
    {
        #region Fields

        private string[] _scopes;
        private GraphOptions _options;
        private IPublicClientApplication _app;
        private IWebWindowManager _webWindowManager;

        #endregion

        #region Constructors

        public GraphService(IWebWindowManager webWindowManager, IOptions<GraphOptions> options)
        {
            _webWindowManager = webWindowManager;
            _options = options.Value;
            _scopes = _options.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            _app = PublicClientApplicationBuilder.Create(_options.ClientId)
                .WithRedirectUri(_options.RedirectUrl)
                .Build();

            TokenCacheHelper.EnableSerialization(_app.UserTokenCache);

            var authProvider = new InteractiveAuthenticationProvider(_app, _scopes);

            this.GraphClient = new GraphServiceClient(authProvider);
        }

        #endregion

        #region Properties

        public IGraphServiceClient GraphClient { get; }

        public bool IsSignedIn => _app.GetAccountsAsync().Result.Any();

        #endregion

        #region Methods

        public async Task SignInAsync()
        {
            var webViewOptions = new SystemWebViewOptions()
            {
                BrowserRedirectSuccess = new Uri(Program.BaseUrl),
                BrowserRedirectError = new Uri(Program.BaseUrl),
                OpenBrowserAsync = uri => this.NavigateToAsync(uri)
            };

            await _app.AcquireTokenInteractive(_scopes)
                    .WithSystemWebViewOptions(webViewOptions)
                    .ExecuteAsync();

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
        IGraphServiceClient GraphClient { get; }

        bool IsSignedIn { get; }

        Task SignInAsync();

        Task SignOutAsync();
    }
}
