using CryptoDrive.ViewModels;

namespace CryptoDrive.Shared
{
	public partial class NavMenu
	{
		#region Constructors

		public NavMenu()
		{
			this.PropertyChanged = async (sender, e) =>
			{
				if (e.PropertyName == nameof(AppStateViewModel.IsSignedIn))
				{
					await this.InvokeAsync(this.StateHasChanged);
				}
			};
		}

		#endregion
	}
}
