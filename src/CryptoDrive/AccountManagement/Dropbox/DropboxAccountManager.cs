using CryptoDrive.Core;
using Dropbox.Api;
using Dropbox.Api.Users;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace CryptoDrive.AccountManagement
{
    public class DropboxAccountManager : IDropboxAccountManager
    {
        #region Fields

        private Uri _host;
        private Uri _redirectUri;
        private Uri _jsRedirectUri;
        private string _cacheFolderPath;
        private DropboxOptions _options;
        private IWebWindowManager _webWindowManager;

        #endregion

        #region Constructors

        public DropboxAccountManager(IOptions<DropboxOptions> options, IWebWindowManager webWindowManager)
        {
            _options = options.Value;
            _webWindowManager = webWindowManager;

            _redirectUri = new Uri(_options.RedirectUrl);
            _host = new Uri(_redirectUri.GetLeftPart(UriPartial.Authority));
            _jsRedirectUri = new Uri(_host, "token");

            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _cacheFolderPath = Path.Combine(localAppDataPath, "CryptoDrive", "Drives", "Dropbox", "TokenCache");

            Directory.CreateDirectory(_cacheFolderPath);
        }

        #endregion

        #region Methods

        public async Task<string> SignInAsync()
        {
            // Dropbox does not use refresh tokens
            // https://stackoverflow.com/questions/40367799/re-usable-refresh-tokens-for-dropbox-api
            var accessToken = await AcquireAccessTokenAsync();

            // Get user information
            using (var dbxClient = new DropboxClient(accessToken))
            {
                var fullAccount = await dbxClient.Users.GetCurrentAccountAsync();

                // cache accessToken
                var cacheFilePath = this.GetCacheFilePath(fullAccount.Email);
                await File.WriteAllTextAsync(cacheFilePath, accessToken);

                // return account name
                return fullAccount.Email;
            }
        }

        public async Task SignOutAsync(string username)
        {
            try
            {
                using var dbxClient = await this.CreateDropboxClientAsync(username);
                await dbxClient.Auth.TokenRevokeAsync();
            }
            catch (Exception)
            {
                // we did our best
            }

            var cacheFilePath = this.GetCacheFilePath(username);

            if (File.Exists(cacheFilePath))
                File.Delete(cacheFilePath);
        }

        public async Task<DropboxClient> CreateDropboxClientAsync(string username)
        {
            var accessToken = await this.AquireAccessTokenSilent(username);
            return new DropboxClient(accessToken);
        }

        private async Task<string> AcquireAccessTokenAsync()
        {
            var state = Guid.NewGuid().ToString();
            var redirectUri = new Uri(_options.RedirectUrl);
            var apiKey = _options.AppKey;
            var authorizeUri = DropboxOAuth2Helper.GetAuthorizeUri(OAuthResponseType.Token, apiKey, redirectUri, state: state);

            var http = new HttpListener();
            http.Prefixes.Add(_host.ToString());
            http.Start();

            try
            {
                _webWindowManager.NavigateToUrl(authorizeUri.ToString());

                // Handle OAuth redirect and send URL fragment to local server using JS.
                await this.HandleOAuth2Redirect(http);

                // Handle redirect from JS and process OAuth response.
                var result = await this.HandleJSRedirect(http);

                if (result.State != state)
                {
                    throw new Exception("Could not retreive the access token.");
                }

                _webWindowManager.NavigateToUrl(Program.BaseUrl);

                return result.AccessToken;
            }
            finally
            {
                http.Stop();
            }            
        }

        private async Task HandleOAuth2Redirect(HttpListener http)
        {
            var context = await http.GetContextAsync();

            // We only care about request to RedirectUri endpoint.
            while (context.Request.Url.AbsolutePath != _redirectUri.AbsolutePath)
            {
                context = await http.GetContextAsync();
            }

            // Respond with a page which runs JS and sends URL fragment as query string
            // to TokenRedirectUri.
            context.Response.ContentType = "text/html";

            var responseContent = this.GetHtmlResponse();

            using (var streamWriter = new StreamWriter(context.Response.OutputStream))
            {
                streamWriter.Write(responseContent);
            }
        }

        private async Task<OAuth2Response> HandleJSRedirect(HttpListener http)
        {
            var context = await http.GetContextAsync();

            // We only care about request to TokenRedirectUri endpoint.
            while (context.Request.Url.AbsolutePath != _jsRedirectUri.AbsolutePath)
            {
                context = await http.GetContextAsync();
            }

            var redirectUri = new Uri(context.Request.QueryString["url_with_fragment"]);
            var result = DropboxOAuth2Helper.ParseTokenFragment(redirectUri);

            return result;
        }

        private string GetHtmlResponse()
        {
            return @"
<html>
<script type=""text/javascript"">
    function redirect() {
        // Append fragment as query string so that server can receive it.
        document.location.href = ""/token?url_with_fragment="" + encodeURIComponent(document.location.href);
    }
</script>
<body onload=""redirect()""/>
</html>
";
        }

        private async Task<string> AquireAccessTokenSilent(string username)
        {
            var cacheFilePath = this.GetCacheFilePath(username);
            var accessToken = await File.ReadAllTextAsync(cacheFilePath);

            return accessToken;
        }

        private string GetCacheFilePath(string username)
        {
            return Path.Combine(_cacheFolderPath, $"{username}.cache"); ;
        }

        #endregion
    }

    public interface IDropboxAccountManager : IAccountManager
    {
        Task<DropboxClient> CreateDropboxClientAsync(string username);
    }
}
