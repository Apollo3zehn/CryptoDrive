using System;
using System.Threading;
using System.Threading.Tasks;
using WebWindows;

namespace CryptoDrive.Core
{ 
    public class WebWindowManager : IWebWindowManager
    {
        #region Fields

        private WebWindow _webWindow;

        #endregion

        #region Constructors

        public WebWindowManager()
        {
            //
        }

        #endregion

        #region Methods

        public void NavigateToUrl(string url)
        {
            if (_webWindow == null)
                this.InstantiateWindow();

            _webWindow.NavigateToUrl(url);
        }

        private void InstantiateWindow()
        {
            var mre = new ManualResetEventSlim(initialState: false);

            Task.Run(() =>
            {
                _webWindow = new WebWindow("Crypto Drive");
                mre.Set();
                _webWindow.WaitForExit();
                Environment.Exit(0);
            });

            mre.Wait();
        }

        #endregion
    }

    public interface IWebWindowManager
    {
        void NavigateToUrl(string url);
    }
}
