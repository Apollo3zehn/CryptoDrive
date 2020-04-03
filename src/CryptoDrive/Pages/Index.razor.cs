using CryptoDrive.ViewModels;

namespace CryptoDrive.Pages
{
    public partial class Index
    {
		#region Constructors

		public Index()
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
