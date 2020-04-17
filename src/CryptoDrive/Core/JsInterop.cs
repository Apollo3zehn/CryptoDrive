using Microsoft.JSInterop;
using System.Threading.Tasks;

namespace CryptoDrive.Core
{
    public static class JsInterop
    {
        #region Methods

        public static async Task CopyToClipboard(IJSRuntime jsRuntime, string value)
        {
            await jsRuntime.InvokeAsync<object>("copyToClipboard", value);
        }

        #endregion
    }
}
