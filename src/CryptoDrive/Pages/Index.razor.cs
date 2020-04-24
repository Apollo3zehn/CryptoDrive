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
				if (e.PropertyName == nameof(AppStateViewModel.ShowSyncFolderAddEditDialog))
					await this.InvokeAsync(this.StateHasChanged);

				else if (e.PropertyName == nameof(AppStateViewModel.ShowSyncFolderRemoveDialog))
					await this.InvokeAsync(this.StateHasChanged);

				else if (e.PropertyName == nameof(AppStateViewModel.Config))
					await this.InvokeAsync(this.StateHasChanged);
			};
		}

		#endregion

		#region Methods

		private void OnActiveIndexChanged(int index)
		{
			this.AppState.ActiveSyncAccount = this.AppState.Config.SyncAccounts[index];
		}

		#endregion
	}
}
