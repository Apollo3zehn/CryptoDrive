using WebWindows.Blazor;

namespace CryptoDrive
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ComponentsDesktop.Run<Startup>("Crypto Drive", "wwwroot/index.html");
        }
    }
}
