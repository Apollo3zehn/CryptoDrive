using CryptoDrive.ViewModels;

namespace CryptoDrive.Shared
{
	public partial class ControlMenu
	{
		#region Constructors

		public ControlMenu()
		{
			this.PropertyChanged = async (sender, e) =>
			{
				if (e.PropertyName == nameof(AppStateViewModel.Config))
				{
					await this.InvokeAsync(this.StateHasChanged);
				}
			};
		}

		#endregion
	}
}
