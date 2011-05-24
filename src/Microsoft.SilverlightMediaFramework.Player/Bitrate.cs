using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Microsoft.SilverlightMediaFramework.Player
{
	[Description("Represents a control that displays the bitrate.")]
	[TemplatePart(Name = ElementName.Percentage0Bar, Type = typeof(FrameworkElement))]
	[TemplatePart(Name = ElementName.Percentage25Bar, Type = typeof(FrameworkElement))]
	[TemplatePart(Name = ElementName.Percentage50Bar, Type = typeof(FrameworkElement))]
	[TemplatePart(Name = ElementName.Percentage75Bar, Type = typeof(FrameworkElement))]
	[TemplatePart(Name = ElementName.HDElement, Type = typeof(FrameworkElement))]
	[TemplateVisualState(Name = VisualStateName.BitratePercentage0, GroupName = "BitrateStates")]
	[TemplateVisualState(Name = VisualStateName.BitratePercentage25, GroupName = "BitrateStates")]
	[TemplateVisualState(Name = VisualStateName.BitratePercentage50, GroupName = "BitrateStates")]
	[TemplateVisualState(Name = VisualStateName.BitratePercentage75, GroupName = "BitrateStates")]
	[TemplateVisualState(Name = VisualStateName.BitratePercentageHD, GroupName = "BitrateStates")]
	public class Bitrate : Control
	{
		// template part names
		private static class ElementName
		{
			public const string Percentage0Bar = "Percentage0Bar";
			public const string Percentage25Bar = "Percentage25Bar";
			public const string Percentage50Bar = "Percentage50Bar";
			public const string Percentage75Bar = "Percentage75Bar";
			public const string HDElement = "HDElement";
		}

		// visual state names
		private static class VisualStateName
		{
			public const string BitratePercentage0 = "BitratePercentage0";
			public const string BitratePercentage25 = "BitratePercentage25";
			public const string BitratePercentage50 = "BitratePercentage50";
			public const string BitratePercentage75 = "BitratePercentage75";
			public const string BitratePercentageHD = "BitratePercentageHD";
		}

		// properties
		
		[Category("Common Properties"), Description("Bitrate value.")]
		public ulong BitrateValue
		{
			get { return (ulong)GetValue(BitrateValueProperty); }
			set { SetValue(BitrateValueProperty, value); }
		}

		public static readonly DependencyProperty BitrateValueProperty =
			DependencyProperty.Register("BitrateValue", typeof(ulong), typeof(Bitrate),
			new PropertyMetadata(OnPropertyChanged));

		[Category("Common Properties"), Description("Maximum bitrate value.")]
		public ulong MaximumBitrate
		{
			get { return (ulong)GetValue(MaximumBitrateProperty); }
			set { SetValue(MaximumBitrateProperty, value); }
		}


        //MaximumBitrate is not being set if the SmoothStreamingSource is being set in XAML
        //As a workaround I have set the default MaximumBitrate to 1500000. This is a short
        //term fix, and eventually the behavior of the Player should be modified to ensure
        //this property is set.  Kevin Rohling 11-9-09 5:17PM
		public static readonly DependencyProperty MaximumBitrateProperty =
			DependencyProperty.Register("MaximumBitrate", typeof(ulong), typeof(Bitrate),
			new PropertyMetadata((ulong)1500000, OnPropertyChanged));

		[Category("Common Properties"), Description("High definition bitrate value.")]
		public ulong HighDefinitionBitrate
		{
			get { return (ulong)GetValue(HighDefinitionBitrateProperty); }
			set { SetValue(HighDefinitionBitrateProperty, value); }
		}

		public static readonly DependencyProperty HighDefinitionBitrateProperty =
			DependencyProperty.Register("HighDefinitionBitrate", typeof(ulong), typeof(Bitrate),
			new PropertyMetadata(OnPropertyChanged));

		public Bitrate()
		{
			DefaultStyleKey = typeof(Bitrate);
		}

		// BitrateValue and MaximumBitrate dependency property callback
		private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			Bitrate source = d as Bitrate;
			source.UpdateVisualState();
		}

		// set visual state based on percentage of BitrateValue to MaximumBitrate
		private void UpdateVisualState()
		{
			// first check for HD state
			if (HighDefinitionBitrate != 0 && BitrateValue >= HighDefinitionBitrate)
			{
				VisualStateManager.GoToState(this, VisualStateName.BitratePercentageHD, true);
				return;
			}

			// 0% if a max bitrate is not specified
			if (MaximumBitrate == 0)
			{
				VisualStateManager.GoToState(this, VisualStateName.BitratePercentage0, true);
				return;
			}

			// determine percentage, and set visual state
			double percentage = (double)BitrateValue / (double)MaximumBitrate;

			// under 25%
			if (percentage < .25)
			{
				VisualStateManager.GoToState(this, VisualStateName.BitratePercentage0, true);
				return;
			}

			// between 25% and 49%
			if (percentage < .50)
			{
				VisualStateManager.GoToState(this, VisualStateName.BitratePercentage25, true);
				return;
			}

			// between 50% and 74%
			if (percentage < .75)
			{
				VisualStateManager.GoToState(this, VisualStateName.BitratePercentage50, true);
				return;
			}

			// over 75%
			VisualStateManager.GoToState(this, VisualStateName.BitratePercentage75, true);
		}
	}
}
