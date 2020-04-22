using CryptoDrive.Core;
using Microsoft.AspNetCore.Components;

namespace CryptoDrive.Views
{
	public partial class SyncFolderPairView
	{
		#region Properties

		[Parameter]
		public SyncFolderPair DataContext { get; set; }

		#endregion
	}
}
