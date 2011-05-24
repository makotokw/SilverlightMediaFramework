using System;
using System.Windows;
using System.ComponentModel;

namespace Microsoft.SilverlightMediaFramework.Player
{
	/// <summary>
	/// Represents metadata associated with a specific point in a media file.
	/// </summary>
	public class Marker : DependencyObject
	{
		private Guid id = Guid.Empty;

		public Marker()
		{
		}

		public static readonly DependencyProperty TimeProperty =
			DependencyProperty.Register("Time", typeof(TimeSpan), typeof(Marker),
			new PropertyMetadata(TimeSpan.Zero));

		public static readonly DependencyProperty DurationProperty =
			DependencyProperty.Register("Duration", typeof(TimeSpan), typeof(Marker),
			new PropertyMetadata(TimeSpan.Zero));

		/// <summary>
		/// Gets the Id of the marker.
		/// </summary>
		//[Description("Id of the marker.")]
		//public Guid Id
		//{
		//    get { return id; }
		//    private set { id = value; }
		//}

		/// <summary>
		/// Gets or sets the time for the marker.
		/// </summary>
		[Description("Time of the marker.")]
		public TimeSpan Time
		{
			get { return (TimeSpan)GetValue(TimeProperty); }
			set { SetValue(TimeProperty, value); }
		}

		/// <summary>
		/// Gets or sets the duration of the marker.
		/// </summary>
		[Description("Duration of the marker.")]
		public TimeSpan Duration
		{
			get { return (TimeSpan)GetValue(DurationProperty); }
			set { SetValue(DurationProperty, value); }
		}

		// Called by MarkerControl.  Usage handled by child class implementation
		public virtual void OnMarkerClicked(object sender)
		{
		}
	}
}
