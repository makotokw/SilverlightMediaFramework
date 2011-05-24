using System.Windows;

namespace Microsoft.SilverlightMediaFramework.Player
{
	public class SnapValueEventArgs : RoutedEventArgs
	{
		public double SnapValue { get; internal set; }
	}
}
