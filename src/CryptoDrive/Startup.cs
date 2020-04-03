using CryptoDrive.Core;
using CryptoDrive.Graph;
using CryptoDrive.ViewModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            services.AddSingleton<IGraphService, GraphService>();
            services.AddSingleton<IWebWindowManager, WebWindowManager>();

            // custom options
            services.Configure<GraphOptions>(this.Configuration.GetSection("Graph"));
        }

        public void Configure(IApplicationBuilder app, IWebWindowManager webWindowManager)
        {
            app.UseDeveloperExceptionPage();

            app.UseStaticFiles();
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
