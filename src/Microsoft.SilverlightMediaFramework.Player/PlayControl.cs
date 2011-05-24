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
using System.Diagnostics;

namespace Microsoft.SilverlightMediaFramework.Player
{
	public enum PlayState { Playing, Paused, Stopped, FastForwarding, SlowMotion, Rewinding }

	//VisualStates
	[TemplateVisualState(Name = CommonStates.Normal, GroupName = "CommonStates")]
	[TemplateVisualState(Name = CommonStates.MouseOver, GroupName = "CommonStates")]
	[TemplateVisualState(Name = CommonStates.Pressed, GroupName = "CommonStates")]
	[TemplateVisualState(Name = CommonStates.Disabled, GroupName = "CommonStates")]

	[TemplateVisualState(Name = "Playing", GroupName = "PlayStates")]
	[TemplateVisualState(Name = "Paused", GroupName = "PlayStates")]
	[TemplateVisualState(Name = "Stopped", GroupName = "PlayStates")]
	[TemplateVisualState(Name = "SlowMotion", GroupName = "PlayStates")]
	[TemplateVisualState(Name = "FastForwarding", GroupName = "PlayStates")]
	[TemplateVisualState(Name = "Rewinding", GroupName = "PlayStates")]

	//Parts
	[TemplatePart(Name = ElementName.PlayControlRootElement, Type = typeof(FrameworkElement))]
	[TemplatePart(Name = ElementName.PlayingElement, Type = typeof(FrameworkElement))]
	[TemplatePart(Name = ElementName.PausedElement, Type = typeof(FrameworkElement))]
	[TemplatePart(Name = ElementName.StoppedElement, Type = typeof(FrameworkElement))]
	[TemplatePart(Name = ElementName.SlowMotionElement, Type = typeof(FrameworkElement))]
	[TemplatePart(Name = ElementName.FastForwardingElement, Type = typeof(FrameworkElement))]
	[TemplatePart(Name = ElementName.RewindingElement, Type = typeof(FrameworkElement))]


	public class PlayControl : Control
	{
		public event RoutedEventHandler PlayStateChanged;
		public event RoutedEventHandler PlayControlClicked;


		#region Constructor / Init

		public PlayControl()
		{
			this.DefaultStyleKey = typeof(PlayControl);
			this.IsEnabledChanged += new DependencyPropertyChangedEventHandler(PlayControl_IsEnabledChanged);
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			_playControlRootElement = GetTemplateChild(ElementName.PlayControlRootElement) as FrameworkElement;
		}


		#endregion Constructor / Init

		#region VisualState & TemplatePart Classes

		// template part names
		private static class ElementName
		{
			public const string PlayControlRootElement = "PlayControlRootElement";
			public const string PlayingElement = "PlayingElement";
			public const string PausedElement = "PausedElement";
			public const string StoppedElement = "StoppedElement";
			public const string SlowMotionElement = "SlowMotionElement";
			public const string FastForwardingElement = "FastForwardingElement";
			public const string RewindingElement = "RewindingElement";
		}

		private static class CommonStates
		{
			public const string Normal = "Normal";
			public const string MouseOver = "MouseOver";
			public const string Pressed = "Pressed";
			public const string Disabled = "Disabled";
		}

		#endregion region VisualState & TemplatePart Classes

		#region Members / Properties

		// TemplateParts
		private FrameworkElement _playControlRootElement;

		public PlayState PreviousPlayState { get; set; }

		public PlayState CurrentPlayState
		{
			get { return (PlayState)GetValue(CurrentPlayStateProperty); }
			set 
			{
				PreviousPlayState = CurrentPlayState;
				SetValue(CurrentPlayStateProperty, value); 
			}
		}

		#region Dependency Properties

		public static readonly DependencyProperty CurrentPlayStateProperty =
			DependencyProperty.Register("CurrentPlayState", typeof(PlayState), typeof(PlayControl),
			new PropertyMetadata(new PropertyChangedCallback(OnCurrentPlayStateChanged)));

		#endregion Dependency Properties

		#endregion Members / Properties

		#region Events


		// Template Control Events

		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonDown(e);

			VisualStateManager.GoToState(this, CommonStates.Pressed, true);
		}

		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonUp(e);

			VisualStateManager.GoToState(this, CommonStates.MouseOver, true);
			ControlHelper.RaiseEvent(PlayControlClicked, this);
		}

		protected override void OnMouseEnter(MouseEventArgs e)
		{
			base.OnMouseEnter(e);
			VisualStateManager.GoToState(this, CommonStates.MouseOver, true);
		}

		protected override void OnMouseLeave(MouseEventArgs e)
		{
			base.OnMouseLeave(e);
			VisualStateManager.GoToState(this, CommonStates.Normal, true);
		}

		// Dependency Property Events

		private static void OnCurrentPlayStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			PlayControl pc = d as PlayControl;
			Debug.Assert(pc != null, "PlayControl is null");

			ControlHelper.RaiseEvent(pc.PlayStateChanged, pc);
			VisualStateManager.GoToState(pc, ((PlayState)e.NewValue).ToString(), true);
		}

		void PlayControl_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			PlayControl pc = sender as PlayControl;
			Debug.Assert(pc != null, "PlayControl is null");
			VisualStateManager.GoToState(pc, ((bool)e.NewValue) ? CommonStates.Normal : CommonStates.Disabled, true);
		}

		#endregion Events
	}
}
