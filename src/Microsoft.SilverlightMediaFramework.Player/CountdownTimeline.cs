using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Globalization;

namespace Microsoft.SilverlightMediaFramework.Player
{
	[TemplatePart(Name = ElementName.ProgressElement, Type = typeof(ProgressBar))]
	[TemplatePart(Name = ElementName.TextElement, Type = typeof(TextBlock))]
	public class CountdownTimeline : Control
	{
		private const double TickInterval = 250;

		private DispatcherTimer timer;
		private int secondsRemaining;

		// template parts
		private ProgressBar progressElement;
		private TextBlock textElement;

		private static class ElementName
		{
			public const string ProgressElement = "ProgressElement";
			public const string TextElement = "TextElement";
		}

		public TimeSpan Duration
		{
			get { return (TimeSpan)GetValue(DurationProperty); }
			set { SetValue(DurationProperty, value); }
		}

		public static readonly DependencyProperty DurationProperty =
			DependencyProperty.Register("Duration", typeof(TimeSpan), typeof(CountdownTimeline),
			new PropertyMetadata(TimeSpan.Zero, OnDurationPropertyChanged));

		public TimeSpan Position
		{
			get { return (TimeSpan)GetValue(PositionProperty); }
			set { SetValue(PositionProperty, value); }
		}

		public static readonly DependencyProperty PositionProperty =
			DependencyProperty.Register("Position", typeof(TimeSpan), typeof(CountdownTimeline),
			new PropertyMetadata(TimeSpan.Zero, OnPositionPropertyChanged));

		public string TextFormat
		{
			get { return (string)GetValue(TextFormatProperty); }
			set { SetValue(TextFormatProperty, value); }
		}

		public static readonly DependencyProperty TextFormatProperty =
			DependencyProperty.Register("TextFormat", typeof(string), typeof(CountdownTimeline),
			new PropertyMetadata(string.Empty, OnTextFormatPropertyChanged));

		private string Text
		{
			set
			{
				if (textElement != null)
				{
					textElement.Text = value;
				}
			}
		}

		private int SecondsRemaining
		{
			get { return secondsRemaining; }
			set
			{
				if (secondsRemaining != value)
				{
					secondsRemaining = value;
					UpdateText();
				}
			}
		}

		public CountdownTimeline()
		{
			this.DefaultStyleKey = typeof(CountdownTimeline);

			timer = new DispatcherTimer();
			timer.Interval = TimeSpan.FromMilliseconds(TickInterval);
			timer.Tick += timer_Tick;
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			progressElement = GetTemplateChild(ElementName.ProgressElement) as ProgressBar;
			if (progressElement != null)
			{
				progressElement.Maximum = Duration.TotalSeconds;
				progressElement.Value = Position.TotalSeconds;
			}

			textElement = GetTemplateChild(ElementName.TextElement) as TextBlock;
			UpdateText();
		}

		private void timer_Tick(object sender, EventArgs e)
		{
			Position = Position.Add(TimeSpan.FromMilliseconds(TickInterval));
		}

		private void UpdateSecondsRemaining()
		{
			TimeSpan remaining = Duration.Subtract(Position);
			SecondsRemaining = (int)Math.Ceiling(remaining.TotalSeconds);
		}

		private void UpdateText()
		{
			Text = string.Format(CultureInfo.CurrentUICulture, TextFormat, SecondsRemaining);
		}

		public void Start()
		{
			Position = TimeSpan.Zero;
			timer.Start();
		}

		public void Stop()
		{
			timer.Stop();
			// commenting this out for now
			// was used previous when we were triggering Start early
			//Text = string.Empty;
		}

		private static void OnDurationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			CountdownTimeline timeline = d as CountdownTimeline;
			if (timeline != null)
			{
				if (timeline.progressElement != null)
				{
					timeline.progressElement.Maximum = ((TimeSpan)e.NewValue).TotalSeconds;
				}

				timeline.Position = TimeSpan.Zero;
				timeline.UpdateSecondsRemaining();
			}
		}

		private static void OnPositionPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			CountdownTimeline timeline = d as CountdownTimeline;
			TimeSpan value = (TimeSpan)e.NewValue;
			if (timeline != null)
			{
				if (timeline.progressElement != null)
				{
					if (value <= timeline.Duration)
					{
						timeline.progressElement.Value = value.TotalSeconds;
					}
					else
					{
						timeline.Position = timeline.Duration;
					}
				}

				timeline.UpdateSecondsRemaining();
			}
		}

		private static void OnTextFormatPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			CountdownTimeline timeline = d as CountdownTimeline;
			if (timeline != null)
			{
				timeline.UpdateText();
			}
		}
	}
}
