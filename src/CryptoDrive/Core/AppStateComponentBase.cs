using CryptoDrive.ViewModels;
using Microsoft.AspNetCore.Components;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace CryptoDrive.Core
{
	public abstract class AppStateComponentBase : ComponentBase, IDisposable
	{
		#region Properties

		[Inject]
		protected AppStateViewModel AppState { get; set; }

		protected PropertyChangedEventHandler PropertyChanged { get; set; }

		#endregion

		#region Methods

		protected override Task OnParametersSetAsync()
		{
			this.AppState.PropertyChanged += this.PropertyChanged;

			return base.OnParametersSetAsync();
		}

		#endregion

		#region IDisposable Support

		private bool disposedValue = false;

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					this.AppState.PropertyChanged -= this.PropertyChanged;
				}

				disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}

		#endregion
	}
}