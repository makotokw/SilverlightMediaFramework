using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Microsoft.SilverlightMediaFramework.Player
{
	[TemplatePart(Name = ElementName.HorizontalTemplate, Type = typeof(Panel))]
	[TemplatePart(Name = ElementName.HorizontalThumb, Type = typeof(Thumb))]
	[TemplatePart(Name = ElementName.HorizontalTrackLargeChangeDecreaseRepeatButton, Type = typeof(RepeatButton))]
	[TemplatePart(Name = ElementName.HorizontalTrackLargeChangeIncreaseRepeatButton, Type = typeof(RepeatButton))]
	[TemplatePart(Name = ElementName.HorizontalAvailableBar, Type = typeof(Border))]
	[TemplatePart(Name = ElementName.MarkersElement, Type = typeof(ItemsControl))]
	[TemplatePart(Name = ElementName.MarkerDisplay, Type = typeof(MarkerControl))]
	[TemplatePart(Name = ElementName.LiveIndicatorElement, Type = typeof(FrameworkElement))]
	[TemplateVisualState(Name = "VOD", GroupName = "LiveStates")]
	[TemplateVisualState(Name = "Live", GroupName = "LiveStates")]
	public class Scrubber : Slider
	{
		// if the mouse is captured, click and dragging on left or right side of thumb
		private bool captured;

		// template controls
		private Panel panel;
		private Thumb thumb;
		private ItemsControl markersElement;
		private Border availableBar;
		private FrameworkElement liveIndicatorElement;

		// template part names
		private static class ElementName
		{
			internal const string HorizontalTemplate = "HorizontalTemplate";
			internal const string HorizontalThumb = "HorizontalThumb";
			internal const string HorizontalTrackLargeChangeDecreaseRepeatButton = "HorizontalTrackLargeChangeDecreaseRepeatButton";
			internal const string HorizontalTrackLargeChangeIncreaseRepeatButton = "HorizontalTrackLargeChangeIncreaseRepeatButton";
			internal const string HorizontalAvailableBar = "HorizontalAvailableBar";
			internal const string MarkersElement = "MarkersElement";
			internal const string MarkerDisplay = "MarkerDisplay";
			internal const string LiveIndicatorElement = "LiveIndicatorElement";
		}

		public event RoutedEventHandler ScrubStarted;
		public event RoutedEventHandler ScrubCompleted;

		// return true if currently dragging, otherwise false
		public bool IsDragging
		{
			get
			{
				// see if dragging thumb, or the left / right areas 
				bool thumbDragging = (thumb == null) ? false : thumb.IsDragging;
				return thumbDragging || captured;
			}
		}

		public double Available
		{
			get { return (double)GetValue(AvailableProperty); }
			set { SetValue(AvailableProperty, value); }
		}

		public static readonly DependencyProperty AvailableProperty =
			DependencyProperty.Register("Available", typeof(double), typeof(Scrubber),
			new PropertyMetadata((double)0, Scrubber.OnAvailablePropertyChanged));

		public Visibility AvailableVisibility
		{
			get { return (Visibility)GetValue(AvailableVisibilityProperty); }
			set { SetValue(AvailableVisibilityProperty, value); }
		}

		public static readonly DependencyProperty AvailableVisibilityProperty =
			DependencyProperty.Register("AvailableVisibility", typeof(Visibility), typeof(Scrubber),
			new PropertyMetadata(Visibility.Collapsed, Scrubber.OnAvailableVisibilityPropertyChanged));

		public new double Minimum
		{
			get
			{
				return base.Minimum;
			}
			
			set
			{
				base.Minimum = value;

				// update the marker data value, necessary so the marker 
				// layout code knows about the new start position
				MarkerData markerData = DataContext as MarkerData;
				if (markerData != null)
				{
					markerData.StartPosition = TimeSpan.FromSeconds(value);	
				}
			}
		}

		public new double Maximum
		{
			get
			{
				return base.Maximum;
			}

			set
			{
				base.Maximum = value;

				// update the marker data value, necessary so the marker 
				// layout code knows about the new end position
				MarkerData markerData = DataContext as MarkerData;
				if (markerData != null)
				{
					markerData.EndPosition = TimeSpan.FromSeconds(value);
				}
			}
		}
		
		public Scrubber()
		{
            this.DefaultStyleKey = typeof(Scrubber);
		}

		protected override void OnValueChanged(double oldValue, double newValue)
		{
			// make sure the new value is within range
			double value = GetInRangeValue(newValue);
			if (value != newValue)
			{
				// not in range, set the value to the max value that is within range
				base.Value = value;
				return;
			}
			
			// within range, pass along to base class
			base.OnValueChanged(oldValue, value);
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			// workaround for mouse events, don't want the left / right areas to 
			// get mouse events since it prevents the position from being updated
			// by clicking in the left / right area and dragging
			DiableMouseEvents(ElementName.HorizontalTrackLargeChangeDecreaseRepeatButton);
			DiableMouseEvents(ElementName.HorizontalTrackLargeChangeIncreaseRepeatButton);
			EnableMouseEvents(ElementName.HorizontalTemplate);

			UninitializeTemplateChildren();
			GetTemplateChildren();
			InitializeTemplateChildren();

			UpdateVisualStates();
		}

		internal void UpdateMarkers()
		{
			// the goal is to execute the MarkerItemsPanel.ArrangeOverride method, but don't
			// have access to MarkerItemsPanel since markersElement is an ItemsControl and the 
			// panel is defined in the template 
			if (markersElement != null)
			{
				// reset the ItemsSource to force the markers to be arranged on the scrubber
				markersElement.ItemsSource = null;
				MarkerData markerData = DataContext as MarkerData;
				if (markerData != null)
					markersElement.ItemsSource = markerData.Markers;
			}
		}

		private void panel_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			// bar size changed, update available bar
			OnAvailableChanged();
		}

		private void GetTemplateChildren()
		{
			panel = GetTemplateChild(ElementName.HorizontalTemplate) as Panel;
			thumb = GetTemplateChild(ElementName.HorizontalThumb) as Thumb;
			markersElement = GetTemplateChild(ElementName.MarkersElement) as ItemsControl;
			availableBar = GetTemplateChild(ElementName.HorizontalAvailableBar) as Border;
			liveIndicatorElement = GetTemplateChild(ElementName.LiveIndicatorElement) as FrameworkElement;
		}

		private void InitializeTemplateChildren()
		{
			// main container
			if (panel != null)
			{
				panel.MouseLeftButtonDown += panel_MouseLeftButtonDown;
				panel.MouseLeftButtonUp += panel_MouseLeftButtonUp;
				panel.MouseMove += panel_MouseMove;
				panel.SizeChanged += panel_SizeChanged;
			}

			// thumb
			if (thumb != null)
			{
				thumb.DragStarted += thumb_DragStarted;
				thumb.DragCompleted += thumb_DragCompleted;
			}

			// markers
			if (markersElement != null)
			{
				MarkerData markerData = DataContext as MarkerData;
				if (markerData != null)
					markersElement.ItemsSource = markerData.Markers;
			}
		}

		private void UninitializeTemplateChildren()
		{
			// main container
			if (panel != null)
			{
				panel.MouseLeftButtonDown -= panel_MouseLeftButtonDown;
				panel.MouseLeftButtonUp -= panel_MouseLeftButtonUp;
				panel.MouseMove -= panel_MouseMove;
				panel.SizeChanged -= panel_SizeChanged;
			}

			// thumb
			if (thumb != null)
			{
				thumb.DragStarted -= thumb_DragStarted;
				thumb.DragCompleted -= thumb_DragCompleted;
			}
		}

		private void thumb_DragStarted(object sender, DragStartedEventArgs e)
		{
			// raise scrub start event
			ControlHelper.RaiseEvent(ScrubStarted, this);
		}

		private void thumb_DragCompleted(object sender, DragCompletedEventArgs e)
		{
			// raise scrub completed event
			ControlHelper.RaiseEvent(ScrubCompleted, this);
		}

		private void panel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			panel.CaptureMouse();
			captured = true;
			UpdatePosition(e);

			ControlHelper.RaiseEvent(ScrubStarted, this);
		}

		private void panel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (captured)
			{
				panel.ReleaseMouseCapture();
				captured = false;

				ControlHelper.RaiseEvent(ScrubCompleted, this);
			}
		}

		private void panel_MouseMove(object sender, MouseEventArgs e)
		{
			if (captured)
			{
				UpdatePosition(e);
			}
		}

		// update the position based on the mouse position
		private void UpdatePosition(MouseEventArgs e)
		{
			// take into account the scrubber thumb size
			double thumbWidth = (thumb == null) ? 0 : thumb.ActualWidth;
			double panelWidth = panel.ActualWidth - thumbWidth;

			if (panelWidth > 0)
			{
				double range = Maximum - Minimum;

				// calculate the new value based on mouse position
				Point position = e.GetPosition(panel);
				double value = (position.X * range) / panelWidth;

				// right now, the thumb will be left-justified to the cursor, take
				// into account the size of the thumb and center it under the cursor
				value -= ((thumbWidth / 2) * range) / panelWidth;

				// offset from the min value
				value += Minimum;

				// make sure within bounds
				value = GetInRangeValue(value);

				// update position
				Value = value;
			}
		}

		// return value that is within range
		private double GetInRangeValue(double value)
		{
			// the max value, use available-time if the available bar is visible
			double maxValue = AvailableVisibility == Visibility.Visible ? Available : Maximum;
			double newValue = Math.Max(value, Minimum);
			newValue = Math.Min(newValue, maxValue);
			return newValue;
		}

		// make sure the specified control does not get mouse events
		private void DiableMouseEvents(string childName)
		{
			Control control = GetTemplateChild(childName) as Control;
			if (control != null)
			{
				control.IsHitTestVisible = false;
			}
		}

		// make sure the specified control does get mouse events
		private void EnableMouseEvents(string childName)
		{
			Panel control = GetTemplateChild(childName) as Panel;
			if (control != null)
			{
				control.IsHitTestVisible = true;
				if (control.Background == null)
				{
					// need a transparent background to get mouse events, not null
					control.Background = new SolidColorBrush(Colors.Transparent);
				}
			}
		}

		private static void OnAvailableVisibilityPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			Scrubber source = d as Scrubber;
			source.OnAvailableVisibilityChanged();
		}

		private static void OnAvailablePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			Scrubber source = d as Scrubber;
			source.OnAvailableChanged();
		}

		private void OnAvailableVisibilityChanged()
		{
			if (availableBar != null)
			{
				availableBar.Visibility = AvailableVisibility;
			}
			
			UpdateVisualStates();
		}

		private void OnAvailableChanged()
		{
			double range = Maximum - Minimum;

			// convert the available unit to a pixel width
			if (AvailableVisibility == Visibility && panel != null &&
				availableBar != null && panel.ActualWidth > 0 && range > 0)
			{
				// calculate the pixel width of the available bar, note that need to take into
				// account the thumb width, otherwise the thumb position is calculated differently
				// then the available bar (the thumb position takes into account the thumb width)
				double availableRange = Available - Minimum;
				double pixelValue = (availableRange / range) * (panel.ActualWidth - thumb.ActualWidth);

				// want the width of the available bar to be aligned with the right of the thumb, this
				// allows the live indictor to be correctly positioned to the right of the thumb
				pixelValue += thumb.ActualWidth;

				// make sure within range, note Available can be negative when live stream first starts
				pixelValue = Math.Min(panel.ActualWidth, pixelValue);
				pixelValue = Math.Max(0, pixelValue);

				availableBar.Width = pixelValue;

				// update the marker object, used by the marker layout code
				MarkerData markerData = DataContext as MarkerData;
				if (markerData != null)
				{
					markerData.LivePosition = TimeSpan.FromSeconds(Available);
				}
			}
		}

		private void UpdateVisualStates()
		{
			// live state
			string liveState = AvailableVisibility == Visibility.Visible ? "Live" : "VOD";
			VisualStateManager.GoToState(this, liveState, true);
		}
	}
}
