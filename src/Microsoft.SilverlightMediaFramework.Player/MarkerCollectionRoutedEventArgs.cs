using System.Windows;
using System.Collections.Generic;

namespace Microsoft.SilverlightMediaFramework.Player
{
	public class MarkerCollectionRoutedEventArgs : RoutedEventArgs
	{
		public List<Marker> Markers { get; set; }
	}
}
