using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Microsoft.SilverlightMediaFramework.Player
{
	// helper class to avoid checking for null when using template elements
	public static class ControlHelper
	{
		public static void CheckToggleButton(ToggleButton control, bool check)
		{
			if (control != null)
			{
				control.IsChecked = check;
			}
		}

		public static void EnableControl(Control control, bool enabled)
		{
			if (control != null)
			{
				control.IsEnabled = enabled;
			}
		}

		public static void SetTextBlockText(TextBlock control, string text)
		{
			if (control != null)
			{
				control.Text = text;
			}
		}

		public static void SetTextBlockTime(TextBlock control, TimeSpan time)
		{
			if (control != null)
			{
				control.Text = String.Format(CultureInfo.InvariantCulture, 
					"{0:00}:{1:00}:{2:00}",	time.Hours, time.Minutes, time.Seconds);
			}
		}

		public static void SetSliderValue(RangeBase control, double value)
		{
			if (control != null)
			{
				control.Value = value;
			}
		}

		public static void RaiseEvent(RoutedEventHandler handler, object sender)
		{
			if (handler != null)
			{
				handler(sender, new RoutedEventArgs());
			}
		}

		public static void RaiseEvent(RoutedEventHandler handler, object sender, RoutedEventArgs args)
		{
			if (handler != null)
			{
				handler(sender, args);
			}
		}

		public static void RaiseEvent(DependencyPropertyChangedEventHandler handler, object sender, DependencyPropertyChangedEventArgs args)
		{
			if (handler != null)
			{
				handler(sender, args);
			}
		}
	}
}
