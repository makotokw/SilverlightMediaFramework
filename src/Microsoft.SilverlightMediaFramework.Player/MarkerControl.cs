using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Microsoft.SilverlightMediaFramework.Player
{
	[TemplatePart(Name = "MarkerElement", Type = typeof(FrameworkElement))]
	[TemplatePart(Name = "MarkerSummary", Type = typeof(ContentControl))]
	[TemplateVisualState(Name = "Normal", GroupName = "CommonStates")]
	[TemplateVisualState(Name = "StateMouseOver", GroupName = "CommonStates")]
	public class MarkerControl : Control
	{
		public string Text
		{
			get { return (string)GetValue(TextProperty); }
			set { SetValue(TextProperty, value); }
		}

		// Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty TextProperty =
			DependencyProperty.Register("Text", typeof(string), typeof(MarkerControl), new PropertyMetadata(null));

		public MarkerControl()
		{
			DefaultStyleKey = typeof(MarkerControl);
		}

		protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
		{
			base.OnMouseEnter(e);
			VisualStateManager.GoToState(this, "StateMouseOver", true);
		}

		protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
		{
			base.OnMouseLeave(e);
			VisualStateManager.GoToState(this, "Normal", true);
		}

		protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
		{
			// OnMouseLeave is not getting called when clicked, so update state here in the click event
			base.OnMouseLeftButtonDown(e);
			VisualStateManager.GoToState(this, "Normal", true);

			if (this.DataContext != null && this.DataContext is Marker)
			{
				e.Handled = true;
				((Marker)DataContext).OnMarkerClicked(this);
			}
		}
	}
}
