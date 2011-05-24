using System;
using System.Collections.ObjectModel;

namespace Microsoft.SilverlightMediaFramework.Player
{
	/// <summary>
	/// Represents marker data.
	/// </summary>
	public class MarkerData : ObservableObject
	{
		private TimeSpan startPosition;
		private TimeSpan endPosition;
		private TimeSpan livePosition;
		private bool live;
		private ObservableCollection<Marker> markers;

		/// <summary>
		/// Gets or sets the list of markers.
		/// </summary>
		public ObservableCollection<Marker> Markers
		{
			get
			{
				return markers;
			}
			
			set
			{
				if (markers != value)
				{
					markers = value;
					OnPropertyChanged("Markers");
				}
			}
		}
		
		/// <summary>
		/// Gets or sets the starting position of the video.
		/// </summary>
		public TimeSpan StartPosition
		{
			get 
			{
				return startPosition; 
			}
			
			set 
			{ 
				if (startPosition != value)
				{
					startPosition = value;
					OnPropertyChanged("StartPosition");
				}
			}
		}

		/// <summary>
		/// Gets or sets the ending position of the video.
		/// </summary>
		public TimeSpan EndPosition
		{
			get
			{
				return endPosition;
			}
			
			set
			{
				if (endPosition != value)
				{
					endPosition = value;
					OnPropertyChanged("EndPosition");
				}
			}
		}

		/// <summary>
		/// Gets or sets the live position of the video.
		/// </summary>
		public TimeSpan LivePosition
		{
			get
			{
				return livePosition;
			}

			set
			{
				if (livePosition != value)
				{
					livePosition = value;
					OnPropertyChanged("LivePosition");
				}
			}
		}

		/// <summary>
		/// Gets or sets if the video is a live feed or not.
		/// </summary>
		public bool IsLive
		{
			get
			{
				return live;
			}

			set
			{
				if (live != value)
				{
					live = value;
					OnPropertyChanged("IsLive");
				}
			}
		}

		public MarkerData()
		{
			Markers = new ObservableCollection<Marker>();
		}
	}
}
