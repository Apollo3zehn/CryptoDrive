using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CryptoDrive
{
    // Electron.NET works good, execpt with MS SSO because Blazor disconnects.
    // - Frame can be hidden
    // - Program
    // - Tray Icon Support
    // https://github.com/ElectronNET/Electron.NET/issues/361
    // https://github.com/ElectronNET/Electron.NET/issues/270

    public class Program
    {
        #region Properties

        public static string BaseUrl { get; private set; }

        #endregion
        
        #region Methods

        public static void Main(string[] args)
        {
            Program.BaseUrl = "http://localhost:34482";
            CreateHostBuilder(args).Build().Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls(Program.BaseUrl);
                })
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole();
                });

        #endregion
    }
}
