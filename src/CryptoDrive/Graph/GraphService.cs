using Microsoft.AspNetCore.Components;
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
        private NavigationManager _navigationManager;

        #endregion

        #region Constructors

        public GraphService(NavigationManager navigationManager, IOptions<GraphOptions> options)
        {
            _navigationManager = navigationManager;
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

        #endregion

        #region Methods

        public async Task<bool> IsRegisteredAsync()
        {
            return (await _app.GetAccountsAsync()).Any();
        }

        public async Task RegisterAsync()
        {
            //var webViewOptions = new SystemWebViewOptions()
            //{
            //    BrowserRedirectSuccess = new Uri("http://app/"),
            //    OpenBrowserAsync = uri => this.NavigateToAsync(uri)
            //};

            //await _app.AcquireTokenInteractive(_scopes)
            //        .WithSystemWebViewOptions(webViewOptions)
            //        .ExecuteAsync();

            await _app
                .AcquireTokenInteractive(_scopes)
                .ExecuteAsync();

            //return await this.GraphClient.Me.Request().GetAsync();
        }

        public async Task UnregisterAsync()
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
            _navigationManager.NavigateTo(uri.ToString());

            return Task.CompletedTask;
        }

        #endregion
    }

    public interface IGraphService
    {
        IGraphServiceClient GraphClient { get; }

        Task<bool> IsRegisteredAsync();

        Task RegisterAsync();

        Task UnregisterAsync();
    }
}
