using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace Microsoft.SilverlightMediaFramework
{
	/// <summary>
	/// Provides an implementation of the INotifyPropertyChanged interface.
	/// </summary>
	public abstract class ObservableObject : INotifyPropertyChanged
	{
		#region Events

		public event PropertyChangedEventHandler PropertyChanged;

		#endregion

		#region Methods

		protected virtual void OnPropertyChanged(string propertyName)
		{
			if (propertyName == null)
				throw new ArgumentNullException("propertyName");

			VerifyProperty(propertyName);

			PropertyChangedEventHandler propertyChangedHandler = PropertyChanged;

			if (propertyChangedHandler != null)
				propertyChangedHandler(this, new PropertyChangedEventArgs(propertyName));
		}

		[Conditional("DEBUG")]
		private void VerifyProperty(string propertyName)
		{
			PropertyInfo property = GetType().GetProperty(propertyName);

			if (property == null)
			{
				string message = string.Format("Invalid property: {0}", propertyName);
				throw new Exception(message);
			}
		}

		#endregion
	}
}