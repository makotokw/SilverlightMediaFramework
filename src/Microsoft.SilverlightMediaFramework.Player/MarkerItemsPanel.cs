using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Microsoft.SilverlightMediaFramework.Player
{
	// layout markers on the timeline
	public class MarkerItemsPanel : Panel
	{
		// width of the thumb in the timeline, required to calculate the position of the markers
		public double ThumbWidth
		{
			get { return (double)GetValue(ThumbWidthProperty); }
			set { SetValue(ThumbWidthProperty, value); }
		}

		public static readonly DependencyProperty ThumbWidthProperty =
			DependencyProperty.Register("ThumbWidth", typeof(double),
			typeof(MarkerItemsPanel), null);

		// start position of the timeline
		private TimeSpan StartPosition
		{
			get { return (TimeSpan)GetValue(StartPositionProperty); }
			set { SetValue(StartPositionProperty, value); }
		}

		public static readonly DependencyProperty StartPositionProperty =
			DependencyProperty.Register("StartPosition", typeof(TimeSpan), typeof(MarkerItemsPanel),
			new PropertyMetadata(MarkerItemsPanel.OnDependencyPropertyChanged));

		// end position of the timeline
		private TimeSpan EndPosition
		{
			get { return (TimeSpan)GetValue(EndPositionProperty); }
			set { SetValue(EndPositionProperty, value); }
		}

		public static readonly DependencyProperty EndPositionProperty =
			DependencyProperty.Register("EndPosition", typeof(TimeSpan), typeof(MarkerItemsPanel),
			new PropertyMetadata(MarkerItemsPanel.OnDependencyPropertyChanged));

		// live position of the timeline
		private TimeSpan LivePosition
		{
			get { return (TimeSpan)GetValue(LivePositionProperty); }
			set { SetValue(LivePositionProperty, value); }
		}

		public static readonly DependencyProperty LivePositionProperty =
			DependencyProperty.Register("LivePosition", typeof(TimeSpan), typeof(MarkerItemsPanel),
			new PropertyMetadata(MarkerItemsPanel.OnDependencyPropertyChanged));

		// video live mode
		private bool IsLive
		{
			get { return (bool)GetValue(IsLiveProperty); }
			set { SetValue(IsLiveProperty, value); }
		}

		public static readonly DependencyProperty IsLiveProperty =
			DependencyProperty.Register("IsLive", typeof(bool), typeof(MarkerItemsPanel),
			new PropertyMetadata(MarkerItemsPanel.OnDependencyPropertyChanged));

		// markers that will be positioned on the timeline
		private ObservableCollection<Marker> Markers
		{
			get { return (ObservableCollection<Marker>)GetValue(MarkersProperty); }
			set { SetValue(MarkersProperty, value); }
		}

		public static readonly DependencyProperty MarkersProperty =
			DependencyProperty.Register("MarkersPosition", typeof(ObservableCollection<Marker>), typeof(MarkerItemsPanel),
			new PropertyMetadata(MarkerItemsPanel.OnMarkersPropertyChanged));

		public MarkerItemsPanel()
		{
			// marker controls are one control with two states, the part
			// displayed on the timeline and the part that contains the content,
			// this creates rounding problems when calculating the position 
			// of the marker, to workaround this, set UseLayoutRounding to false
			UseLayoutRounding = false;

			// wireup binding to get notified when markers should be arranged on timeline
			InitializeBindings();
		}

		// this is an odd control, we don't want to force the consumer to remember to 
		// specify binding in XAML, so manually wireup binding in code behind, this is
		// necessary so this control is called when data values change, then it can 
		// relayout the markers on the timeline
		private void InitializeBindings()
		{
			Binding startPositionBinding;
			startPositionBinding = new Binding("StartPosition");
			startPositionBinding.Source = DataContext;
			startPositionBinding.Mode = BindingMode.OneWay;
			SetBinding(MarkerItemsPanel.StartPositionProperty, startPositionBinding);

			Binding endPositionBinding;
			endPositionBinding = new Binding("EndPosition");
			endPositionBinding.Source = DataContext;
			endPositionBinding.Mode = BindingMode.OneWay;
			SetBinding(MarkerItemsPanel.EndPositionProperty, endPositionBinding);

			Binding livePositionBinding;
			livePositionBinding = new Binding("LivePosition");
			livePositionBinding.Source = DataContext;
			livePositionBinding.Mode = BindingMode.OneWay;
			SetBinding(MarkerItemsPanel.LivePositionProperty, livePositionBinding);

			Binding liveBinding;
			liveBinding = new Binding("IsLive");
			liveBinding.Source = DataContext;
			liveBinding.Mode = BindingMode.OneWay;
			SetBinding(MarkerItemsPanel.IsLiveProperty, liveBinding);

			Binding markersBinding;
			markersBinding = new Binding("Markers");
			markersBinding.Source = DataContext;
			markersBinding.Mode = BindingMode.OneWay;
			SetBinding(MarkerItemsPanel.MarkersProperty, markersBinding);
		}

		// dependency property notification
		private static void OnDependencyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			MarkerItemsPanel source = d as MarkerItemsPanel;
			source.InvalidateArrange();
		}

		// dependency property notification
		private static void OnMarkersPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			MarkerItemsPanel source = d as MarkerItemsPanel;
			source.OnMarkersChanged(e.OldValue);
		}

		private void OnMarkersChanged(object oldValue)
		{
			// the main marker list changed, first unhook events from previous list
			ObservableCollection<Marker> oldMarkers = oldValue as ObservableCollection<Marker>;
			if (oldMarkers != null)
			{
				oldMarkers.CollectionChanged -= Markers_CollectionChanged;
			}

			// hook event for new list
			if (Markers != null)
			{
				Markers.CollectionChanged += Markers_CollectionChanged;
			}

			// update markers on timeline
			InvalidateArrange();
		}

		private void Markers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			// update markers on timeline
			InvalidateArrange();
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			// this is the first pass in the layout, each control updates
			// its DesiredSize property, which is used later in ArrangeOverride
			foreach (UIElement child in Children)
			{
				child.Measure(availableSize);
			}

			return base.MeasureOverride(availableSize);
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			// nothing to do if don't have any marker data
			MarkerData markerData = DataContext as MarkerData;
			if (markerData == null)
			{
				return base.ArrangeOverride(finalSize);
			}
			
			// use live position when in live, otherwise end position, 
			// this is the bounds where markers are visisble
			TimeSpan endPosition = IsLive ? markerData.LivePosition : markerData.EndPosition;

			// nothing to do if the timeline does not have a range
			double range = markerData.EndPosition.TotalSeconds - markerData.StartPosition.TotalSeconds;
			if (range <= 0)
			{
				return base.ArrangeOverride(finalSize);
			}

			// go through each marker control and layout on the timeline
			foreach (FrameworkElement control in Children)
			{
				// marker data, Marker.Time is absolute time
				Marker marker = (Marker)control.DataContext;

				// make sure the marker is within the timeline range
				if (marker.Time < markerData.StartPosition || marker.Time > endPosition)
				{
					// don't display the marker
					control.Arrange(new Rect(0, 0, 0, 0));
				}
				else
				{
					// convert the absolute time to a relative timeline time
					TimeSpan time = marker.Time - markerData.StartPosition;

					// calculate the top position, center the marker vertically
					double top = (finalSize.Height - control.DesiredSize.Height) / 2;

					// calculate the left position, first get the pixel position within the timeline
					double left = (time.TotalSeconds * (finalSize.Width - ThumbWidth)) / range;

					// next adjust the position so the center of the control is at the time on the timeline,
					// note that the marker control can overhang the left or right side of the timeline
					left += ((ThumbWidth - control.DesiredSize.Width) / 2);

					// display the marker
					control.Arrange(new Rect(left, top, control.DesiredSize.Width, control.DesiredSize.Height));
				}
			}

			return base.ArrangeOverride(finalSize);
		}
	}
}
