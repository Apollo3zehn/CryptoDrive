using CryptoDrive.ViewModels;

namespace CryptoDrive.Shared
{
	public partial class LoginDisplay
	{
		#region Constructors

		public LoginDisplay()
		{
			this.PropertyChanged = async (sender, e) =>
			{
				if (e.PropertyName == nameof(AppStateViewModel.IsSignedIn))
				{
					await this.InvokeAsync(this.StateHasChanged);
				}
				else if (e.PropertyName == nameof(AppStateViewModel.GivenName))
				{
					await this.InvokeAsync(this.StateHasChanged);
				}
			};
		}

		#endregion
	}
}
