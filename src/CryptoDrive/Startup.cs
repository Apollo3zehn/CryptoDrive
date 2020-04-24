using CryptoDrive.AccountManagement;
using CryptoDrive.Core;
using CryptoDrive.ViewModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System;
using System.IO;

namespace CryptoDrive
{
    public class Startup
    {
        #region Constructors

        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        #endregion

        #region Properties

        public IConfiguration Configuration { get; }

        #endregion

        #region Methods

        public void ConfigureServices(IServiceCollection services)
        {
            // blazor
            services.AddRazorPages();
            services.AddServerSideBlazor();

            // custom services
            services.AddSingleton<AppStateViewModel>();
            services.AddSingleton<CryptoDriveContext>();
            services.AddSingleton<IWebWindowManager, WebWindowManager>();

            // driver provider
            services.AddSingleton<IOneDriveService, OneDriveAccountManager>();
            services.Configure<OneDriveOptions>(this.Configuration.GetSection("OneDrive"));

            services.AddSingleton<IGoogleDriveAccountManager, GoogleDriveAccountManager>();

            services.AddSingleton<IDropboxAccountManager, DropboxAccountManager>();
            services.Configure<DropboxOptions>(this.Configuration.GetSection("Dropbox"));
        }

        public void Configure(IApplicationBuilder app, IWebWindowManager webWindowManager)
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logFolderPath = Path.Combine(localAppDataPath, "CryptoDrive", "Logs");
            Directory.CreateDirectory(logFolderPath);

            app.UseDeveloperExceptionPage();

            app.UseStaticFiles();
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(logFolderPath),
                RequestPath = "/logs"
            });

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });

            webWindowManager.NavigateToUrl(Program.BaseUrl);
        }

        #endregion
    }
}
