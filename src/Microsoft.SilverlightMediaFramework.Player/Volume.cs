using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Globalization;

namespace Microsoft.SilverlightMediaFramework.Player
{

	//VisualStates
	[TemplateVisualState(Name = CommonStates.Normal, GroupName = "CommonStates")]
	[TemplateVisualState(Name = CommonStates.MouseOver, GroupName = "CommonStates")]
	[TemplateVisualState(Name = CommonStates.Pressed, GroupName = "CommonStates")]
	[TemplateVisualState(Name = CommonStates.Disabled, GroupName = "CommonStates")]
	[TemplateVisualState(Name = MutedStates.VolumeOn, GroupName = "MuteStates")]
	[TemplateVisualState(Name = MutedStates.Muted, GroupName = "MuteStates")]
	[TemplateVisualState(Name = ExpandedStates.Expanded, GroupName = "ExpandedStates")]
	[TemplateVisualState(Name = ExpandedStates.Collapsed, GroupName = "ExpandedStates")]

	//Parts
	[TemplatePart(Name = ElementName.SliderElement, Type = typeof(Slider))]
	[TemplatePart(Name = ElementName.BaseElement, Type = typeof(FrameworkElement))]
	[TemplatePart(Name = ElementName.ExpandingElement, Type = typeof(FrameworkElement))]

	public class VolumeControl : Control
	{
		public event DependencyPropertyChangedEventHandler VolumeLevelChanged;
		public event RoutedEventHandler MutedStateClicked;
		public event RoutedEventHandler UnMutedStateClicked;

		#region Constructor / Init

		public VolumeControl()
		{
			this.DefaultStyleKey = typeof(VolumeControl);
			this.IsEnabledChanged += new DependencyPropertyChangedEventHandler(VolumeControl_IsEnabledChanged);

			Application.Current.Host.Content.FullScreenChanged += OnFullScreenChanged;
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			IsExpanded = false;

			UninitializeTemplateChildren();
			GetTemplateChildren();
			InitializeTemplateChildren();


			if (VolumeLevel == 0)
			{
				this.IsMuted = true;
				this.VolumeLevelBeforeMuted = .5;
			}

			VisualStateManager.GoToState(this, IsMuted ? MutedStates.Muted : MutedStates.VolumeOn, true);
		}

		private void GetTemplateChildren()
		{
			_slider = GetTemplateChild(ElementName.SliderElement) as Slider;
			_baseElement = GetTemplateChild(ElementName.BaseElement) as FrameworkElement;
			_expandingElement = GetTemplateChild(ElementName.ExpandingElement) as FrameworkElement;
		}

		private void InitializeTemplateChildren()
		{
			if (_slider != null)
			{
				Binding sliderBinding = new Binding("VolumeLevel");
				sliderBinding.Source = this;
				sliderBinding.Mode = BindingMode.TwoWay;
				_slider.SetBinding(Slider.ValueProperty, sliderBinding);
			}

			if (_baseElement != null)
			{
				_baseElement.MouseLeftButtonDown += _baseElement_MouseLeftButtonDown;
				_baseElement.MouseLeftButtonUp += _baseElement_MouseLeftButtonUp;
			}
		}

		private void UninitializeTemplateChildren()
		{
			if (_baseElement != null)
			{
				_baseElement.MouseLeftButtonDown -= _baseElement_MouseLeftButtonDown;
				_baseElement.MouseLeftButtonUp -= _baseElement_MouseLeftButtonUp;
			}
		}

		#endregion Constructor / Init

		#region VisualState & TemplatePart Classes

		// template part names
		private static class ElementName
		{
			public const string SliderElement = "SliderElement";
			public const string BaseElement = "BaseElement";
			public const string ExpandingElement = "ExpandingElement";
		}

		private static class CommonStates
		{
			public const string Normal = "Normal";
			public const string Disabled = "Disabled";
			public const string Pressed = "Pressed";
			public const string MouseOver = "MouseOver";
		}

		private static class MutedStates
		{
			public const string Muted = "Muted";
			public const string VolumeOn = "VolumeOn";
		}

		private static class ExpandedStates
		{
			public const string Expanded = "Expanded";
			public const string Collapsed = "Collapsed";
		}

		#endregion region VisualState & TemplatePart Classes

		#region Members / Properties

		// TemplateParts
		private Slider _slider;
		private FrameworkElement _baseElement;
		private FrameworkElement _expandingElement;

		public static double DefaultVolumeLevel = .5;

		private bool _bExpandOnMouseOver = true;
		public bool ExpandOnMouseOver
		{
			get
			{
				return _bExpandOnMouseOver;
			}
			set
			{
				_bExpandOnMouseOver = value;
			}
		}

		private bool _bAnimateVolume = true;
		public bool AnimateVolume
		{
			get
			{
				return _bAnimateVolume;
			}
			set
			{
				_bAnimateVolume = value;
			}
		}

		public bool IsMuted
		{
			get { return (bool)GetValue(IsMutedProperty); }
			set
			{
				SetValue(IsMutedProperty, value);
			}
		}

		public bool IsExpanded
		{
			get { return (bool)GetValue(IsExpandedProperty); }
			set { SetValue(IsExpandedProperty, value); }
		}

		private double VolumeLevelBeforeMuted { get; set; }

		public double VolumeLevel
		{
			get { return (double)GetValue(VolumeProperty); }
			set { SetValue(VolumeProperty, value); }
		}

		#region Dependency Properties

		public static readonly DependencyProperty VolumeProperty =
			DependencyProperty.Register("VolumeLevel", typeof(double), typeof(VolumeControl),
			new PropertyMetadata(DefaultVolumeLevel, new PropertyChangedCallback(OnVolumeLevelChanged)));

		public static readonly DependencyProperty IsMutedProperty =
			DependencyProperty.Register("IsMuted", typeof(bool), typeof(VolumeControl),
			new PropertyMetadata(new PropertyChangedCallback(OnIsMutedChanged)));

		public static readonly DependencyProperty IsExpandedProperty =
			DependencyProperty.Register("IsExpanded", typeof(bool), typeof(VolumeControl),
			new PropertyMetadata(new PropertyChangedCallback(OnIsExpandedChanged)));

		#endregion Dependency Properties

		// ExpandingElementFullHeight is used to determine if transition to expanded state has completed
		private double _expandingElementFullHeight = 100;
		public double ExpandingElementFullHeight
		{
			get
			{
				return _expandingElementFullHeight;
			}
			set
			{
				_expandingElementFullHeight = value;
			}
		}
		#endregion Members / Properties

		#region Events

		// Template Control Events

		void VolumeControl_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			VolumeControl vc = sender as VolumeControl;
			Debug.Assert(vc != null, "PlayControl is null");
			
			if (!IsEnabled && IsExpanded)
				IsExpanded = false;

			VisualStateManager.GoToState(vc, ((bool)e.NewValue) ? CommonStates.Normal : CommonStates.Disabled, true);
		}

		protected override void OnMouseEnter(MouseEventArgs e)
		{
			base.OnMouseEnter(e);

			VisualStateManager.GoToState(this, CommonStates.MouseOver, true);

			if (ExpandOnMouseOver && IsEnabled)
				IsExpanded = true;
		}

		protected override void OnMouseLeave(MouseEventArgs e)
		{
			base.OnMouseLeave(e);

			VisualStateManager.GoToState(this, CommonStates.Normal, true);

			if (ExpandOnMouseOver && IsEnabled)
			{

				// Default behavior when changing states is to stop the existing animation / transition.  So if 
				// we have a delay before closing, and the user mouses in and out real quick, the volume will partially
				// open and be stuck in that state because of the delay before closing.  This code will force the expand
				// animation to complete (.1 seconds), then trigger the close
				if (_expandingElement != null && _expandingElement.Height < ExpandingElementFullHeight)
				{
					DoubleAnimationHelper(
						_expandingElement.Height,
						ExpandingElementFullHeight, 
						.15, 
						null,
						((Object sender, EventArgs args) => IsExpanded = false), 
						_expandingElement, 
						"Height");
				}
				else
				{
					IsExpanded = false;
				}
			}
		}

		void _baseElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			VisualStateManager.GoToState(this, CommonStates.Pressed, true);

			if (IsMuted)
			{
				// Going from Muted to UnMuted
				IsMuted = false;
				ControlHelper.RaiseEvent(UnMutedStateClicked, this);
			}
			else
			{
				// Going from UnMuted to Muted
				IsMuted = true;
				ControlHelper.RaiseEvent(MutedStateClicked, this);
			}
		}

		void _baseElement_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			VisualStateManager.GoToState(this, CommonStates.MouseOver, true);
		}

		// Dependency Property Events

		private static void OnVolumeLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			VolumeControl vc = d as VolumeControl;
			Debug.Assert(vc != null, "VolumeControl is null");
			if (vc._slider != null)
			{
				if (!vc.isAnimating)
				{
					if (vc.IsMuted)
					{
						// User is interacting w/ slider while Muted.  Turn off IsMuted and set
						// VolumeLevelBeforeMuted to -1 so OnIsMutedChanged doesn't animate to pre-Mute Volume
						vc.VolumeLevelBeforeMuted = -1;
						vc.IsMuted = false;
						ControlHelper.RaiseEvent(vc.UnMutedStateClicked, vc);
					}
					else if (vc.VolumeLevel == 0)
					{
						// user scrubbed to 0.  Need to mock a Muted state.  Set the pre mute level to .5 so they
						// have somewhere to go if they click "unmute"
						vc.IsMuted = true;
						vc.VolumeLevelBeforeMuted = .5;
					}
				}
				ControlHelper.RaiseEvent(vc.VolumeLevelChanged, vc, e);
			}
			else
			{
				if (!vc.IsMuted && vc.VolumeLevel == 0)
				{
					vc.IsMuted = true;
					vc.VolumeLevelBeforeMuted = .5;
				}
			}
		}

		private static void OnIsMutedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			VolumeControl vc = d as VolumeControl;
			Debug.Assert(vc != null, "VolumeControl is null");

			if ((bool)e.NewValue)
			{
				// preserve current volume
				vc.VolumeLevelBeforeMuted = vc.VolumeLevel;

				// Set VolumeLevel to 0 for Muting
				if (vc.AnimateVolume)
					vc.DoubleAnimationHelper(vc.VolumeLevel, 0, .1, null, null, vc, "VolumeLevel");
				else
					vc.VolumeLevel = 0;
			}
			else
			{
				if (vc.VolumeLevelBeforeMuted > 0)
				{
					// restore previous volume
					if (vc.AnimateVolume)
						vc.DoubleAnimationHelper(vc.VolumeLevel, vc.VolumeLevelBeforeMuted, .1, null, null, vc, "VolumeLevel");
					else
						vc.VolumeLevel = vc.VolumeLevelBeforeMuted;
				}
			}


			VisualStateManager.GoToState(vc, ((bool)e.NewValue) ? MutedStates.Muted : MutedStates.VolumeOn, true);
		}

		private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			VolumeControl vc = d as VolumeControl;
			Debug.Assert(vc != null, "VolumeControl is null");
			VisualStateManager.GoToState(vc, ((bool)e.NewValue) ? ExpandedStates.Expanded : ExpandedStates.Collapsed, true);
		}

		#endregion Events

		#region Worker Methods

		private bool isAnimating = false;
		public void DoubleAnimationHelper(double from, double to, double? seconds, double? speedRatio, System.EventHandler onCompleted, DependencyObject obj, string property)
		{
			isAnimating = true;
			Storyboard sb = new Storyboard();

			if (onCompleted != null)
				sb.Completed += new EventHandler(onCompleted);

			DoubleAnimation animation = new DoubleAnimation();
			animation.From = from;
			animation.To = to;

			if (speedRatio == null)
			{
				animation.Duration = TimeSpan.FromSeconds(seconds == null ? 1 : (double)seconds);
			}
			else
			{
				animation.SpeedRatio = (double)speedRatio;
			}

			Storyboard.SetTarget(animation, obj);
			Storyboard.SetTargetProperty(animation, new PropertyPath(string.Format(CultureInfo.InvariantCulture, "({0})", property), new object[0]));
			sb.Children.Add(animation);
			sb.Completed += ((Object sender, EventArgs args) => isAnimating = false);
			sb.Begin();
		}

		#endregion Worker Methods

		void OnFullScreenChanged(object sender, EventArgs e)
		{
			if (!Application.Current.Host.Content.IsFullScreen)
			{
				IsExpanded = false;
			}
		}
	}
}
