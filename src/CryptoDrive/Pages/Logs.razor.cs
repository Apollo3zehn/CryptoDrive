using CryptoDrive.Core;
using CryptoDrive.ViewModels;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;

namespace CryptoDrive.Pages
{
	public partial class Logs
    {
		#region Constructors

		public Logs()
		{
			this.LogLevelValues = Utilities
				.GetEnumValues<LogLevel>()
				.Where(logLevel => logLevel < LogLevel.None)
				.ToList();

			this.PropertyChanged = async (sender, e) =>
			{
				if (e.PropertyName == nameof(AppStateViewModel.MessageLog))
					await this.InvokeAsync(this.StateHasChanged);
			};
		}

		#endregion

		#region Properties

		public List<LogLevel> LogLevelValues { get; }

		#endregion
	}
}
