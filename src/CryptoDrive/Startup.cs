using CryptoDrive.Core;
using CryptoDrive.Graph;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using WebWindows.Blazor;

namespace CryptoDrive
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // logging
            services.AddLogging(logging =>
            {
                logging.AddConsole();
                logging.AddDebug();
            });

            // custom services
            services.AddSingleton<CryptoDriveContext>();
            services.AddSingleton<IGraphService, GraphService>();

            // configuration (workaround)
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true);

            var configuration = configurationBuilder.Build();

            // custom options
            services.Configure<GraphOptions>(configuration.GetSection("Graph"));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(DesktopApplicationBuilder app)
        {
            app.AddComponent<App>("app");
        }
    }
}
