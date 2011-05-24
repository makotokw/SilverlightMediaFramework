using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Browser;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using Microsoft.SilverlightMediaFramework.Logging;
using Microsoft.Web.Media.Diagnostics;
using Microsoft.Web.Media.SmoothStreaming;

namespace Microsoft.SilverlightMediaFramework.Player
{
    [Description("Represents a control that plays videos.")]
    [TemplatePart(Name = ElementName.MediaPresenterElement, Type = typeof(ContentPresenter))]
    //Commented out playSpeedElement because the RotaryControl
    //will not be publicly available for the upcoming PDC release of the SMF.
    //Kevin Rohling 11-10-2009 12:12PM
    //[TemplatePart(Name = ElementName.PlaySpeedElement, Type = typeof(RotaryControl))]
    [TemplatePart(Name = ElementName.PlayElement, Type = typeof(PlayControl))]
    [TemplatePart(Name = ElementName.FullScreenElement, Type = typeof(ToggleButton))]
    [TemplatePart(Name = ElementName.CurrentTimeElement, Type = typeof(TextBlock))]
    [TemplatePart(Name = ElementName.TotalTimeElement, Type = typeof(TextBlock))]
    [TemplatePart(Name = ElementName.PositionElement, Type = typeof(Scrubber))]
    [TemplatePart(Name = ElementName.VolumeElement, Type = typeof(VolumeControl))]
    [TemplatePart(Name = ElementName.RewindElement, Type = typeof(RepeatButton))]
    [TemplatePart(Name = ElementName.FastForwardElement, Type = typeof(RepeatButton))]
    [TemplatePart(Name = ElementName.ReplayElement, Type = typeof(Button))]
    [TemplatePart(Name = ElementName.SlowMotionElement, Type = typeof(ToggleButton))]
    [TemplatePart(Name = ElementName.ChapterMarkerNextElement, Type = typeof(Button))]
    [TemplatePart(Name = ElementName.ChapterMarkerPreviousElement, Type = typeof(Button))]
    [TemplatePart(Name = ElementName.GoToLiveElement, Type = typeof(ToggleButton))]
    [TemplatePart(Name = ElementName.BitrateElement, Type = typeof(Bitrate))]
    [TemplatePart(Name = ElementName.BufferingElement, Type = typeof(FrameworkElement))]
    [TemplatePart(Name = ElementName.CountdownElement, Type = typeof(CountdownTimeline))]
    [TemplateVisualState(Name = "Normal", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "MouseOver", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "AdPlaying", GroupName = "AdStates")]
    [TemplateVisualState(Name = "AdCompleted", GroupName = "AdStates")]
    [TemplateVisualState(Name = "VOD", GroupName = "LiveStates")]
    [TemplateVisualState(Name = "Live", GroupName = "LiveStates")]
    [TemplateVisualState(Name = "GoToLive", GroupName = "LiveStates")]
    [TemplateVisualState(Name = "FullScreenMode", GroupName = "FullScreenStates")]
    [TemplateVisualState(Name = "NormalMode", GroupName = "FullScreenStates")]
    public partial class Player : ContentControl
    {
        // position timer interval in milliseconds
        private const double PositionUpdateInterval = 500;
        // polling timer interval in milliseconds
        private const double PollingUpdateInterval = 750;

        // buffer in milliseconds for use when detecting if an unnatural polling span has occurred
        private const double PollingIntervalBuffer = 250;

        // the SSME has an internal limit where the the playback rate cannot be negative
        static private TimeSpan RewindLimit = TimeSpan.FromSeconds(30);

        // flag, if the SSME can rewind the current position
        private bool CanRewind
        {
            get
            {
                if (mediaElement == null)
                    return false;

                return (mediaElement.Position > RewindLimit);
            }
        }


        // timer, used to update position text, scrubber
        private DispatcherTimer statusTimer;
        // timer, used to poll for markers
        private DispatcherTimer pollingTimer;

        // stores the last polling position for marker checks
        private TimeSpan lastPollingPosition = TimeSpan.Zero;

        // current live mode state
        private LiveModeState liveMode;

        // trick FF / RW work around
        protected PlayState previousPlayState = PlayState.Stopped;
        private double previousVolumeLevel = VolumeControl.DefaultVolumeLevel;
        private bool isTrickFastForwardMode = false;
        private bool isTrickRewindMode = false;

        // template controls
        protected CoreSmoothStreamingMediaElement mediaElement;
        protected ContentPresenter mediaPresenterElement;
        //Commented out playSpeedElement because the RotaryControl
        //will not be publicly available for the upcoming PDC release of the SMF.
        //Kevin Rohling 11-10-2009 12:12PM
        //private RotaryControl playSpeedElement;
        protected PlayControl playElement;
        protected ToggleButton fullScreenElement;
        protected TextBlock currentTimeElement;
        protected TextBlock totalTimeElement;
        protected Scrubber positionElement;
        protected VolumeControl volumeElement;
        protected Bitrate bitrateElement;
        protected Button chapterNextElement;
        protected Button chapterPreviousElement;
        protected ToggleButton slowMotionElement;
        protected ButtonBase fastForwardElement;
        protected ButtonBase rewindElement;
        protected FrameworkElement bufferingElement;
        protected CountdownTimeline countdownElement;
        protected ToggleButton liveElement;
        protected Button replayElement;
        private InStreamDataCollection inStreamData;

        private bool ignoreLiveElementClick;
        private bool ignoreSlowMotionClick;

        // template part names
        private static class ElementName
        {
            public const string MediaPresenterElement = "MediaPresenterElement";
            public const string PlaySpeedElement = "PlaySpeedElement";
            public const string PlayElement = "PlayElement";
            public const string FullScreenElement = "FullScreenElement";
            public const string CurrentTimeElement = "CurrentTimeElement";
            public const string TotalTimeElement = "TotalTimeElement";
            public const string PositionElement = "PositionElement";
            public const string VolumeElement = "VolumeElement";
            public const string RewindElement = "RewindElement";
            public const string FastForwardElement = "FastForwardElement";
            public const string ReplayElement = "ReplayElement";
            public const string SlowMotionElement = "SlowMotionElement";
            public const string ChapterMarkerNextElement = "ChapterMarkerNextElement";
            public const string ChapterMarkerPreviousElement = "ChapterMarkerPreviousElement";
            public const string GoToLiveElement = "GoToLiveElement";
            public const string BitrateElement = "BitrateElement";
            public const string BufferingElement = "BufferingElement";
            public const string CountdownElement = "CountdownElement";
        }

        private enum LiveModeState
        {
            None,		// media element does not exists
            VOD,		// playing a VOD video
            Live,		// playing a live video, at the live position
            GoToLive	// playing a live video, not at the live position
        }

        // events

        public event MarkerRoutedEventHandler MarkerReached;
        public event MarkerRoutedEventHandler MarkerSkippedInto;
        public event MarkerCollectionRoutedEventHandler MarkersSkipped;
        public event RoutedEventHandler IsAdPlayingChanged;

        public event PlayStateChangedRoutedEventHandler PlayStateChanged;
        public event RoutedEventHandler PlayControlClicked;

        //Added JumpedToLive event Kevin Rohling 11-10-2009
        public event RoutedEventHandler JumpedToLive;
        public event RoutedEventHandler MediaFailed;

        public event RoutedEventHandler FullScreenClicked;
        public event RoutedEventHandler RestoreScreenClicked;
        public event RoutedEventHandler MuteClicked;
        public event RoutedEventHandler UnMuteClicked;
        public event RoutedEventHandler PreviousChapterClicked;
        public event RoutedEventHandler NextChapterClicked;

        // determine if should use scrubber position overrides
        private bool IsOverrideScrubberRange
        {
            get
            {
                return (ScrubberEndPosition != TimeSpan.Zero &&
                    ScrubberEndPosition > ScrubberStartPosition);
            }
        }

        private LiveModeState LiveMode
        {
            get
            {
                return liveMode;
            }

            set
            {
                if (liveMode != value)
                {
                    liveMode = value;

                    // make sure playing back at 1x speed if in live-mode					
                    if (liveMode == LiveModeState.Live)
                    {
                        if (mediaElement != null)
                            mediaElement.SetPlaybackRate(1.0);
                    }

                    // update the visual state
                    VisualStateManager.GoToState(this, liveMode.ToString(), true);

                    // update toggle state of live button, checked if 'is live',
                    // unchecked for 'not live' and any other state
                    ignoreLiveElementClick = true;
                    ControlHelper.CheckToggleButton(liveElement, liveMode == LiveModeState.Live);
                    ignoreLiveElementClick = false;

                    // only enable live button when in go-to-live mode
                    ControlHelper.EnableControl(liveElement, liveMode == LiveModeState.GoToLive);

                    // hide the available bar when in VOD mode
                    if (positionElement != null && liveMode == LiveModeState.VOD)
                    {
                        positionElement.Available = 0;
                        positionElement.AvailableVisibility = Visibility.Collapsed;
                    }

                    // show the available bar when in live mode
                    if (positionElement != null && (liveMode == LiveModeState.Live || liveMode == LiveModeState.GoToLive))
                    {
                        positionElement.AvailableVisibility = Visibility.Visible;
                    }

                    // update marker data, so the layout code knows about the state
                    if (MarkerData != null)
                    {
                        MarkerData.IsLive = (liveMode == LiveModeState.Live || liveMode == LiveModeState.GoToLive);
                    }
                }
            }
        }

        public InStreamDataCollection InStreamData
        {
            get
            {
                if (inStreamData == null)
                {
                    inStreamData = new InStreamDataCollection();
                }

                return inStreamData;
            }
            private set
            {
                inStreamData = value;
            }
        }

        [Category("Media"), Description("Media element.")]
        public CoreSmoothStreamingMediaElement MediaElement
        {
            get
            {
                return mediaElement;
            }
            set
            {
                if (mediaElement != null && mediaElement != value)
                {
                    UninitializeMediaElement();
                }

                if (mediaElement != value)
                {
                    mediaElement = value;
                    InitializeMediaElement();
                }
            }
        }

        [Category("Media"), Description("List of markers for the media source."), EditorBrowsable(EditorBrowsableState.Advanced)]
        public MarkerData MarkerData
        {
            get { return (MarkerData)GetValue(MarkerDataProperty); }
            set { SetValue(MarkerDataProperty, value); }
        }

        public static readonly DependencyProperty MarkerDataProperty =
            DependencyProperty.Register("MarkerData", typeof(MarkerData), typeof(Player),
            new PropertyMetadata(Player.OnMarkerDataPropertyChanged));

        [Category("Media"), Description("Indicates if should seek to a new video position while scrubbing. Otherwise seeks to a new position after scrubbing.")]
        public bool SeekWhileScrubbing
        {
            get { return (bool)GetValue(SeekWhileScrubbingProperty); }
            set { SetValue(SeekWhileScrubbingProperty, value); }
        }

        public static readonly DependencyProperty SeekWhileScrubbingProperty =
            DependencyProperty.Register("SeekWhileScrubbing", typeof(bool), typeof(Player),
            new PropertyMetadata(false));

        [Category("Media"), Description("Indicates if an ad is currently playing.")]
        public bool IsAdPlaying
        {
            get { return (bool)GetValue(IsAdPlayingProperty); }
            set { SetValue(IsAdPlayingProperty, value); }
        }

        public static readonly DependencyProperty IsAdPlayingProperty =
            DependencyProperty.Register("IsAdPlaying", typeof(bool), typeof(Player),
            new PropertyMetadata(false, Player.OnIsAdPlayingPropertyChanged));

        [Category("Media"), Description("The estimated duration of the live video.")]
        public TimeSpan LiveDuration
        {
            get { return (TimeSpan)GetValue(LiveDurationProperty); }
            set { SetValue(LiveDurationProperty, value); }
        }

        // TODO: get from setting, change default back to 0
        public static readonly DependencyProperty LiveDurationProperty =
            DependencyProperty.Register("LiveDuration", typeof(TimeSpan), typeof(Player),
            new PropertyMetadata(TimeSpan.Zero, Player.OnLiveDurationPropertyChanged));

        [Category("Media"), Description("Override the starting position of the scrubber.")]
        public TimeSpan ScrubberStartPosition
        {
            get { return (TimeSpan)GetValue(ScrubberStartPositionProperty); }
            set { SetValue(ScrubberStartPositionProperty, value); }
        }

        public static readonly DependencyProperty ScrubberStartPositionProperty =
            DependencyProperty.Register("ScrubberStartPosition", typeof(TimeSpan), typeof(Player),
            new PropertyMetadata(TimeSpan.Zero, Player.OnScrubberRangeOverridePropertyChanged));


        //Added UseSeekingBehavior -Kevin Rohling 11/11/2009
        //This property will be
        //defaulted to true in the original release of SMF because the public
        //Beta version of the SSME does not support true FF and RW behavior.
        /// <summary>
        ///Causes FF/RW to do single step seeks using the TimeSpan specified
        ///in the SeekingInterval property.
        /// </summary>
        [Category("Media"), Description("Changes FF/RW behavior to single step seeks.")]
        public bool UseSeekingBehavior
        {
            get { return (bool)GetValue(UseSeekingBehaviorProperty); }
            set { SetValue(UseSeekingBehaviorProperty, value); }
        }

        public static readonly DependencyProperty UseSeekingBehaviorProperty =
            DependencyProperty.Register("UseSeekingBehavior", typeof(bool), typeof(Player),
            new PropertyMetadata(true));

        //Added SeekingInterval -Kevin Rohling 11/11/2009
        /// <summary>
        /// Specifies the interval to use when UseSeekingBehavior is true.
        /// </summary>
        [Category("Media"), Description("Specifies the interval to use when UseSeekingBehavior is true.")]
        public TimeSpan SeekingInterval
        {
            get { return (TimeSpan)GetValue(SeekingIntervalProperty); }
            set { SetValue(SeekingIntervalProperty, value); }
        }

        public static readonly DependencyProperty SeekingIntervalProperty =
            DependencyProperty.Register("SeekingInterval", typeof(TimeSpan), typeof(Player),
            new PropertyMetadata(TimeSpan.FromSeconds(10)));


        [Category("Media"), Description("Override the ending position of the scrubber.")]
        public TimeSpan ScrubberEndPosition
        {
            get { return (TimeSpan)GetValue(ScrubberEndPositionProperty); }
            set { SetValue(ScrubberEndPositionProperty, value); }
        }

        public static readonly DependencyProperty ScrubberEndPositionProperty =
            DependencyProperty.Register("ScrubberEndPosition", typeof(TimeSpan), typeof(Player),
            new PropertyMetadata(TimeSpan.Zero, Player.OnScrubberRangeOverridePropertyChanged));

        [Category("Media"), Description("If the player will display in VOD mode all of the time.")]
        public bool AlwaysDisplayVOD
        {
            get { return (bool)GetValue(AlwaysDisplayVODProperty); }
            set { SetValue(AlwaysDisplayVODProperty, value); }
        }

        public static readonly DependencyProperty AlwaysDisplayVODProperty =
            DependencyProperty.Register("AlwaysDisplayVOD", typeof(bool), typeof(Player),
            new PropertyMetadata(false, Player.OnAlwaysDisplayVODPropertyChanged));

        [Category("Media"), Description("The amount to increase the duration when the live duration is exceeded.")]
        public double LiveDurationExtendPercentage
        {
            get { return (double)GetValue(LiveDurationExtendPercentageProperty); }
            set { SetValue(LiveDurationExtendPercentageProperty, value); }
        }

        public static readonly DependencyProperty LiveDurationExtendPercentageProperty =
            DependencyProperty.Register("LiveDurationExtendPercentage", typeof(double), typeof(Player),
            new PropertyMetadata(0.15, Player.OnLiveDurationExtendPercentagePropertyChanged));

        [Category("Media"), Description("The amount of seconds when click replay.")]
        public double ReplaySeconds
        {
            get { return (double)GetValue(ReplaySecondsProperty); }
            set { SetValue(ReplaySecondsProperty, value); }
        }

        public static readonly DependencyProperty ReplaySecondsProperty =
            DependencyProperty.Register("ReplaySeconds", typeof(double), typeof(Player),
            new PropertyMetadata((double)5));


        [Category("Media"), Description("The bitrate that is high definition.")]
        public ulong HighDefinitionBitrate
        {
            get { return (ulong)GetValue(HighDefinitionBitrateProperty); }
            set { SetValue(HighDefinitionBitrateProperty, value); }
        }

        public static readonly DependencyProperty HighDefinitionBitrateProperty =
            DependencyProperty.Register("HighDefinitionBitrate", typeof(ulong), typeof(Player),
            new PropertyMetadata((ulong)3450000, Player.OnHighDefinitionBitratePropertyChanged));

        [Category("Media"), Description("The amount of seconds when click fastforward.")]
        public double FastForwardJumpSeconds
        {
            get { return (double)GetValue(FastForwardJumpSecondsProperty); }
            set { SetValue(FastForwardJumpSecondsProperty, value); }
        }

        public static readonly DependencyProperty FastForwardJumpSecondsProperty =
            DependencyProperty.Register("FastForwardJumpSeconds", typeof(double), typeof(Player),
            new PropertyMetadata((double)5));


        [Category("Media"), Description("The amount of seconds when click rewind.")]
        public double RewindJumpSeconds
        {
            get { return (double)GetValue(RewindJumpSecondsProperty); }
            set { SetValue(RewindJumpSecondsProperty, value); }
        }

        public static readonly DependencyProperty RewindJumpSecondsProperty =
            DependencyProperty.Register("RewindJumpSeconds", typeof(double), typeof(Player),
            new PropertyMetadata((double)5));


        [Category("Media"), Description("The interval to update FF / RW.")]
        public double RepeatIntervalForFFRWInSeconds
        {
            get { return (double)GetValue(RepeatIntervalForFFRWInSecondsProperty); }
            set { SetValue(RepeatIntervalForFFRWInSecondsProperty, value); }
        }

        public static readonly DependencyProperty RepeatIntervalForFFRWInSecondsProperty =
            DependencyProperty.Register("RepeatIntervalForFFRWInSeconds", typeof(double), typeof(Player),
            new PropertyMetadata((double).25));

        [Category("Media"), Description("Indicates if ads can be played.")]
        public bool CanPlayAds
        {
            get
            {
#if RELEASE
				return true;
#endif

                return (bool)GetValue(CanPlayAdsProperty);
            }
            set { SetValue(CanPlayAdsProperty, value); }
        }

        public static readonly DependencyProperty CanPlayAdsProperty =
            DependencyProperty.Register("CanPlayAds", typeof(bool), typeof(Player),
            new PropertyMetadata(true));

        [Category("Media"), Description("If should process all data streams.")]
        public bool UseAllDataStreams
        {
            get { return (bool)GetValue(UseAllDataStreamsProperty); }
            set { SetValue(UseAllDataStreamsProperty, value); }
        }

        public static readonly DependencyProperty UseAllDataStreamsProperty =
          DependencyProperty.Register("UseAllDataStreams", typeof(bool), typeof(Player),
          new PropertyMetadata(true, Player.OnUseAllDataStreamsPropertyChanged));


        public Player()
        {
            DefaultStyleKey = typeof(Player);

            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                // initialize the status timer
                statusTimer = new DispatcherTimer();
                statusTimer.Interval = TimeSpan.FromMilliseconds(PositionUpdateInterval);
                statusTimer.Tick += statusTimer_Tick;

                // initialize the polling timer
                pollingTimer = new DispatcherTimer();
                pollingTimer.Interval = TimeSpan.FromMilliseconds(PollingUpdateInterval);
                pollingTimer.Tick += pollingTimer_Tick;
            }
        }

        protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            VisualStateManager.GoToState(this, "MouseOver", true);
        }

        protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            VisualStateManager.GoToState(this, "Normal", true);
        }

        protected override void OnContentChanged(object oldContent, object newContent)
        {
            base.OnContentChanged(oldContent, newContent);

            // content changed, update the new media element
            CoreSmoothStreamingMediaElement newMediaElement = newContent as CoreSmoothStreamingMediaElement;
            if (newContent != null)
            {
                MediaElement = newMediaElement;
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // template controls
            UninitializeTemplateChildren();
            GetTemplateChildren();
            InitializeTemplateChildren();

            // media element
            UninitializeMediaElement();
            InitializeMediaElement();

            OnMarkerDataChanged();
            OnHighDefinitionBitrateChanged();
            OnUseAllDataStreamsChanged();

            InitializeControls();

            UpdateFullScreenVisualState();

            // check if specified SSME logging, note the SSME will 
            // not enable logging if call this from the constructor
            CheckToEnableLogging();

        }

        private void OnUseAllDataStreamsChanged()
        {
            InStreamData.UseAllDataStreams = UseAllDataStreams;
        }

        private static void OnUseAllDataStreamsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Player source = d as Player;
            source.OnUseAllDataStreamsChanged();
        }


        // wireup template elements
        private void GetTemplateChildren()
        {
            mediaPresenterElement = GetTemplateChild(ElementName.MediaPresenterElement) as ContentPresenter;
            //Commented out playSpeedElement because the RotaryControl
            //will not be publicly available for the upcoming PDC release of the SMF.
            //Kevin Rohling 11-10-2009 12:12PM
            //playSpeedElement = GetTemplateChild(ElementName.PlaySpeedElement) as RotaryControl;
            playElement = GetTemplateChild(ElementName.PlayElement) as PlayControl;
            fullScreenElement = GetTemplateChild(ElementName.FullScreenElement) as ToggleButton;
            positionElement = GetTemplateChild(ElementName.PositionElement) as Scrubber;
            volumeElement = GetTemplateChild(ElementName.VolumeElement) as VolumeControl;
            currentTimeElement = GetTemplateChild(ElementName.CurrentTimeElement) as TextBlock;
            totalTimeElement = GetTemplateChild(ElementName.TotalTimeElement) as TextBlock;
            bitrateElement = GetTemplateChild(ElementName.BitrateElement) as Bitrate;
            chapterPreviousElement = GetTemplateChild(ElementName.ChapterMarkerPreviousElement) as Button;
            chapterNextElement = GetTemplateChild(ElementName.ChapterMarkerNextElement) as Button;
            slowMotionElement = GetTemplateChild(ElementName.SlowMotionElement) as ToggleButton;
            bufferingElement = GetTemplateChild(ElementName.BufferingElement) as FrameworkElement;
            countdownElement = GetTemplateChild(ElementName.CountdownElement) as CountdownTimeline;
            fastForwardElement = GetTemplateChild(ElementName.FastForwardElement) as ButtonBase;
            rewindElement = GetTemplateChild(ElementName.RewindElement) as ButtonBase;
            liveElement = GetTemplateChild(ElementName.GoToLiveElement) as ToggleButton;
            replayElement = GetTemplateChild(ElementName.ReplayElement) as Button;
        }

        // add child event handlers
        private void InitializeTemplateChildren()
        {
            // play / pause button
            if (playElement != null)
            {
                playElement.PlayStateChanged += playElement_PlayStateChanged;
                playElement.PlayControlClicked += playElement_PlayControlClicked;
                playElement.CurrentPlayState = PlayState.Stopped;
            }

            // fullscreen button
            if (fullScreenElement != null)
            {
                fullScreenElement.Checked += fullScreenElement_Checked;
                fullScreenElement.Unchecked += fullScreenElement_Unchecked;
                Application.Current.Host.Content.FullScreenChanged += Application_FullScreenChanged;
            }

            // position scrubber
            if (positionElement != null)
            {
                positionElement.ValueChanged += positionElement_ValueChanged;
                positionElement.ScrubStarted += positionElement_ScrubStarted;
                positionElement.ScrubCompleted += positionElement_ScrubCompleted;
            }

            // volume control
            if (volumeElement != null)
            {
                volumeElement.VolumeLevelChanged += volumeElement_VolumeLevelChanged;
                volumeElement.MutedStateClicked += volumeElement_MutedStateClicked;
                volumeElement.UnMutedStateClicked += volumeElement_UnMutedStateClicked;

                if (mediaElement != null)
                {
                    Binding b;
                    b = new Binding("VolumeLevel");
                    b.Source = this;
                    b.Mode = BindingMode.TwoWay;
                    volumeElement.SetBinding(VolumeControl.VolumeProperty, b);

                    b = new Binding("VolumeLevel");
                    b.Source = this;
                    b.Mode = BindingMode.TwoWay;
                    mediaElement.SetBinding(CoreSmoothStreamingMediaElement.VolumeProperty, b);
                }
            }

            //Commented out playSpeedElement because the RotaryControl
            //will not be publicly available for the upcoming PDC release of the SMF.
            //Kevin Rohling 11-10-2009 12:12PM
            //if (playSpeedElement != null && mediaElement !=null)
            //{
            //    // set the snapvalues
            //    var list = from d in mediaElement.SupportedPlaybackRates
            //                where d >= 1.0 || d <= -1.0
            //                orderby d descending select d;

            //    playSpeedElement.SetSnapValues(list.ToList());
            //    playSpeedElement.SnapValueChanged += playSpeedElement_SnapValueChanged;
            //}

            // previous chapter button
            if (chapterPreviousElement != null)
            {
                // hookup event handlers
                chapterPreviousElement.Click += chapterPreviousElement_Click;
            }

            // next chapter button
            if (chapterNextElement != null)
            {
                // hookup event handlers
                chapterNextElement.Click += chapterNextElement_Click;
            }

            // slow motion button
            if (slowMotionElement != null)
            {
                // hookup event handlers
                slowMotionElement.Checked += slowMotionElement_Checked;
                slowMotionElement.Unchecked += slowMotionElement_Unchecked;
            }

            if (fastForwardElement != null)
            {
                fastForwardElement.Click += fastForwardElement_Click;
            }

            if (rewindElement != null)
            {
                rewindElement.Click += rewindElement_Click;
            }

            if (liveElement != null)
            {
                liveElement.Checked += liveElement_Checked;
            }

            if (replayElement != null)
            {
                replayElement.Click += replayElement_Click;
            }
        }

        // remove child event handlers
        private void UninitializeTemplateChildren()
        {
            // play / pause button
            if (playElement != null)
            {
                playElement.PlayStateChanged -= playElement_PlayStateChanged;
                playElement.PlayControlClicked -= playElement_PlayControlClicked;
            }

            // fullscreen button
            if (fullScreenElement != null)
            {
                fullScreenElement.Checked -= fullScreenElement_Checked;
                fullScreenElement.Unchecked -= fullScreenElement_Unchecked;
                Application.Current.Host.Content.FullScreenChanged -= Application_FullScreenChanged;
            }

            // position scrubber
            if (positionElement != null)
            {
                positionElement.ValueChanged -= positionElement_ValueChanged;
                positionElement.ScrubStarted -= positionElement_ScrubStarted;
                positionElement.ScrubCompleted -= positionElement_ScrubCompleted;
            }

            // volume control
            if (volumeElement != null)
            {
                volumeElement.VolumeLevelChanged -= volumeElement_VolumeLevelChanged;
                volumeElement.MutedStateClicked -= volumeElement_MutedStateClicked;
                volumeElement.UnMutedStateClicked -= volumeElement_UnMutedStateClicked;
            }

            // previous chapter button
            if (chapterPreviousElement != null)
            {
                chapterPreviousElement.Click -= chapterPreviousElement_Click;
            }

            // next chapter button
            if (chapterNextElement != null)
            {
                chapterNextElement.Click -= chapterNextElement_Click;
            }

            // slow motion button
            if (slowMotionElement != null)
            {
                slowMotionElement.Checked -= slowMotionElement_Checked;
                slowMotionElement.Unchecked -= slowMotionElement_Unchecked;
            }

            if (fastForwardElement != null)
            {
                fastForwardElement.Click -= fastForwardElement_Click;
            }

            if (rewindElement != null)
            {
                rewindElement.Click -= rewindElement_Click;
            }

            if (liveElement != null)
            {
                liveElement.Checked -= liveElement_Checked;
            }

            if (replayElement != null)
            {
                replayElement.Click -= replayElement_Click;
            }
        }

        private void fullScreenElement_Unchecked(object sender, RoutedEventArgs e)
        {
            // restore (non fullscreen) mode
            Application.Current.Host.Content.IsFullScreen = false;
            ControlHelper.RaiseEvent(RestoreScreenClicked, this);
        }

        private void fullScreenElement_Checked(object sender, RoutedEventArgs e)
        {
            // fullscreen mode
            Application.Current.Host.Content.IsFullScreen = true;
            ControlHelper.RaiseEvent(FullScreenClicked, this);
        }

        private void Application_FullScreenChanged(object sender, EventArgs e)
        {
            // fullscreen mode changed, update the button checked state
            ControlHelper.CheckToggleButton(fullScreenElement, Application.Current.Host.Content.IsFullScreen);
            Logger.Log(new PlayerLog(PlayerLogType.FullScreenChanged) { Sender = this, Message = "FullScreenChanged" });
            UpdateFullScreenVisualState();
        }

        private void UpdateFullScreenVisualState()
        {
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                string state = Application.Current.Host.Content.IsFullScreen ? "FullScreenMode" : "NormalMode";
                VisualStateManager.GoToState(this, state, true);
            }
        }

        private void statusTimer_Tick(object sender, EventArgs e)
        {
            UpdateDisplay();
        }

        private void pollingTimer_Tick(object sender, EventArgs e)
        {
            CheckForReachedMarkers();
        }

        private void playElement_PlayControlClicked(object sender, RoutedEventArgs e)
        {
            switch (mediaElement.CurrentPlaybackState)
            {
                case PlaybackState.Playing:
                    mediaElement.SetPlaybackRate(1);
                    if (mediaElement.CanPause)
                        mediaElement.Pause();
                    break;

                default:
                    mediaElement.Play();
                    mediaElement.SetPlaybackRate(1);
                    break;
            }

            ControlHelper.RaiseEvent(PlayControlClicked, this);
            Logger.Log(new PlayerLog(PlayerLogType.PlayControlClicked, mediaElement.CurrentPlaybackState) { Sender = this, Message = "PlayControlClicked" });
        }

        private void playSpeedElement_SnapValueChanged(object sender, SnapValueEventArgs e)
        {
            if (mediaElement == null)
                return;

            // new playback rate to use
            double rate = e.SnapValue;

            // fast forward, can only fast forward when not in live mode
            if (rate > 1.0 && LiveMode != LiveModeState.Live)
                mediaElement.SetPlaybackRate(rate);

            // always set for rewind and normal playback speeds
            if (rate <= 1.0 && CanRewind)
                mediaElement.SetPlaybackRate(rate);

            // if not specifying standard playback, make sure the video is playing
            if (rate != 1.0)
                mediaElement.Play();
        }

        // SSME has a rewind limit where the playback rate cannot be set to a
        // negative value, disable the rewind control if within the limit
        private void UpdateRewindButton()
        {
            if (mediaElement != null && rewindElement != null)
            {
                rewindElement.IsEnabled = CanRewind;
            }
        }


        private void playElement_PlayStateChanged(object sender, RoutedEventArgs e)
        {
            if (this.PlayStateChanged != null)
            {
                var args = new PlayStateChangedRoutedEventArgs
                {
                    CurrentPlayState = this.playElement.CurrentPlayState,
                    PreviousPlayState = this.playElement.PreviousPlayState
                };

                this.PlayStateChanged(this, args);
            }
        }

        public double VolumeLevel
        {
            get { return (double)GetValue(VolumeLevelProperty); }
            set { SetValue(VolumeLevelProperty, value); }
        }

        public static readonly DependencyProperty VolumeLevelProperty =
            DependencyProperty.Register("VolumeLevel", typeof(double), typeof(Player), new PropertyMetadata(VolumeControl.DefaultVolumeLevel, OnVolumeLevelPropertyChanged));

        private static void OnVolumeLevelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
        }

        private void volumeElement_VolumeLevelChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Logger.Log(new PlayerLog(PlayerLogType.VolumeLevelChanged, e.OldValue, e.NewValue) { Sender = this, Message = "VolumeLevelChanged" });
        }

        private void volumeElement_UnMutedStateClicked(object sender, RoutedEventArgs e)
        {
            // unmute clicked
            if (mediaElement != null)
            {
                mediaElement.IsMuted = false;
            }

            ControlHelper.RaiseEvent(UnMuteClicked, this);

            Logger.Log(new PlayerLog(PlayerLogType.UnMuteClicked) { Sender = this, Message = "UnMuteClicked" });
        }

        private void volumeElement_MutedStateClicked(object sender, RoutedEventArgs e)
        {
            // mute clicked
            if (mediaElement != null)
            {
                mediaElement.IsMuted = true;
            }

            ControlHelper.RaiseEvent(MuteClicked, this);

            Logger.Log(new PlayerLog(PlayerLogType.MuteClicked) { Sender = this, Message = "MuteClicked" });
        }

        protected virtual void mediaElement_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            // update buffering display
            UpdateBufferingState();
        }

        // Eventually, CurrentStateChanged & CurrentPlaybackStateChanged may be able to be merged...
        private void mediaElement_CurrentPlaybackStateChanged(object sender, RoutedEventArgs e)
        {
            switch (mediaElement.CurrentPlaybackState)
            {
                case PlaybackState.Playing:
                    if (playElement != null)
                        playElement.CurrentPlayState = PlayState.Playing;
                    statusTimer.Start();
                    pollingTimer.Start();
                    UpdateDisplay();
                    break;
                case PlaybackState.Paused:
                    if (playElement != null)
                        playElement.CurrentPlayState = PlayState.Paused;
                    UpdateDisplay();
                    pollingTimer.Stop();
                    break;
                case PlaybackState.FastForwarding:
                    if (playElement != null)
                        playElement.CurrentPlayState = PlayState.FastForwarding;
                    pollingTimer.Stop();
                    break;
                case PlaybackState.Rewinding:
                    if (playElement != null)
                        playElement.CurrentPlayState = PlayState.Rewinding;
                    pollingTimer.Stop();
                    break;
                case PlaybackState.SlowMotionPlayback:
                    if (playElement != null)
                        playElement.CurrentPlayState = PlayState.SlowMotion;
                    pollingTimer.Stop();
                    break;
                case PlaybackState.Stopped:
                    if (playElement != null)
                        playElement.CurrentPlayState = PlayState.Stopped;
                    pollingTimer.Stop();
                    break;
                case PlaybackState.Scrubbing:
                    // will this be tied to IsScrubbing?
                    // if so we can move the polling timer stop here rather than in the scrub events
                    break;
                case PlaybackState.Buffering:
                    // don't update the play state for buffering
                    break;
                default:
                    if (playElement != null)
                        playElement.CurrentPlayState = PlayState.Paused;
                    break;
            }
        }

        private void mediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            EnableControls(true);
            statusTimer.Start();

            // initialize volume
            if (volumeElement != null)
            {
                mediaElement.Volume = volumeElement.VolumeLevel;
            }

            // enable the Live button if using a live stream
            if (liveElement != null && mediaElement != null)
            {
                liveElement.IsEnabled = mediaElement.IsLive;
            }

            // Default playback rate
            mediaElement.SetPlaybackRate(1);

            // initialize scrubber
            if (positionElement != null)
            {
                // min / max values
                UpdatePositionScrubberRange();

                // update large / small change values
                double maxValue = mediaElement.EstimatedDuration.TotalSeconds;
                positionElement.LargeChange = maxValue / 10;
                positionElement.SmallChange = maxValue / 100;

                // start at the beginning
                positionElement.Value = 0;
            }

            // initialize the control to the correct live mode visual state
            UpdateLiveMode();

            // make sure not displaying buffering indicator
            UpdateBufferingState(false);

            Logger.Log(new PlayerLog(PlayerLogType.MediaOpened) { Sender = MediaElement, Message = "MediaOpened" });
        }

        private void mediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            // stop the timer
            statusTimer.Stop();
            pollingTimer.Stop();

            if (playElement != null)
            {
                // display Play button
                playElement.CurrentPlayState = PlayState.Stopped;
            }

            // make sure Live button is disabled
            if (liveElement != null)
            {
                liveElement.IsEnabled = false;
            }

            // update display when reaches end of video
            UpdateDisplayMediaEnded();

            // make sure not displaying buffering indicator
            UpdateBufferingState(false);

            // reset playback speed when video ends
            if (mediaElement != null)
                mediaElement.SetPlaybackRate(1.0);

            // need to reset the rotary dial is the user is still interacting with
            // it, this returns to the default position and removes mouse capture
            //if (playSpeedElement != null && playSpeedElement.IsMouseCaptured)
            //Commented out playSpeedElement because the RotaryControl
            //will not be publicly available for the upcoming PDC release of the SMF.
            //Kevin Rohling 11-10-2009 12:12PM
            //    playSpeedElement.Reset();

            Logger.Log(new PlayerLog(PlayerLogType.MediaEnded) { Sender = MediaElement, Message = "MediaEnded" });
        }

        private void mediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            EnableControls(false);
            statusTimer.Stop();
            pollingTimer.Stop();

            // make sure not displaying buffering indicator
            UpdateBufferingState(false);

            // make sure Live button is disabled
            if (liveElement != null)
            {
                liveElement.IsEnabled = false;
            }

            if (this.MediaFailed != null)
            {
                this.MediaFailed(this, new RoutedEventArgs());
            }
        }

        private void positionElement_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // see if changed due to user scrubbing
            if (positionElement.IsDragging)
            {
                if (SeekWhileScrubbing)
                {
                    // update the video position, which also updates 
                    // the current position display
                    UpdateVideoPosition(e.NewValue);
                }
                else
                {
                    // don't update the video position, only update
                    // the current position display, the video
                    // position is updated when scrubbing completes
                    UpdateCurrentPositionDisplay(TimeSpan.FromSeconds(e.NewValue));
                }
            }
        }

        private void positionElement_ScrubStarted(object sender, RoutedEventArgs e)
        {
            // tell the SSME control that we are scrubbing
            if (mediaElement != null)
                mediaElement.Scrubbing = true;

            // disable polling during scrub
            // will this eventually be encapsulated in CurrentPlaybackStateChanged?
            pollingTimer.Stop();

            Logger.Log(new PlayerLog(PlayerLogType.ScrubStarted) { Sender = this, Message = "ScrubStarted" });
        }

        private void positionElement_ScrubCompleted(object sender, RoutedEventArgs e)
        {
            if (!SeekWhileScrubbing)
            {
                // the video position was not updated while
                // scrubbing, update the position now
                UpdateVideoPosition(positionElement.Value);
            }

            // tell the SSME control that we are not scrubbing
            if (mediaElement != null)
                mediaElement.Scrubbing = false;

            // start polling again as long as we're in a Playing state
            // will this eventually be encapsulated in CurrentPlaybackStateChanged?
            if (mediaElement != null && mediaElement.CurrentPlaybackState == PlaybackState.Playing)
            {
                pollingTimer.Start();
            }

            Logger.Log(new PlayerLog(PlayerLogType.ScrubCompleted) { Sender = this, Message = "ScrubCompleted" });
        }

        private void mediaElement_PlaybackBitrateChanged(object sender, RoutedEventArgs e)
        {
            if (bitrateElement != null && mediaElement != null)
            {
                bitrateElement.BitrateValue = mediaElement.PlaybackBitrate;
            }

            Logger.Log(new PlayerLog(PlayerLogType.DownloadBitrateChange)
            {
                Sender = MediaElement,
                Message = string.Format(CultureInfo.InvariantCulture, "PlaybackBitrateChanged: {0}",
                    mediaElement.PlaybackBitrate.ToString(CultureInfo.InvariantCulture))
            });
        }

        private void mediaElement_MaximumPlaybackBitrateChanged(object sender, RoutedEventArgs e)
        {
            if (bitrateElement != null && mediaElement != null)
            {
                bitrateElement.MaximumBitrate = mediaElement.MaximumPlaybackBitrate;
            }

            Logger.Log(new PlayerLog(PlayerLogType.DownloadBitrateChange)
            {
                Sender = MediaElement,
                Message = string.Format(CultureInfo.InvariantCulture, "MaximumPlaybackBitrateChanged: {0}",
                    mediaElement.MaximumPlaybackBitrate.ToString(CultureInfo.InvariantCulture))
            });
        }

        private void mediaElement_PlaybackRateChanged(object sender, RoutedEventArgs e)
        {
            // the playback rate changed, need to update the rotary dial,
            // don't process if the user is interacting with the rotary dial
            //Commented out playSpeedElement because the RotaryControl
            //will not be publicly available for the upcoming PDC release of the SMF.
            //Kevin Rohling 11-10-2009 12:12PM
            //if (mediaElement != null && playSpeedElement != null && !playSpeedElement.IsMouseCaptured)
            //{
            //    // the rotary dial does not have a value for slow motion (0.5),
            //    // use the default angle (1.0) for slow motion
            //    double angleRate = (mediaElement.PlaybackRate == 0.5) ? 1.0 : mediaElement.PlaybackRate;
            //    playSpeedElement.SetAngleFromValue(angleRate);
            //}

            // need to update the checked state of the slow motion button
            if (slowMotionElement != null)
            {
                // wrap in flag so it does not excute the code to 
                // set the playback rate again
                ignoreSlowMotionClick = true;
                slowMotionElement.IsChecked = (mediaElement.PlaybackRate == 0.5);
                ignoreSlowMotionClick = false;
            }
        }

        private void slowMotionElement_Checked(object sender, RoutedEventArgs e)
        {
            // don't process if set the checked state through code, not user interaction
            if (ignoreSlowMotionClick)
                return;

            if (mediaElement != null && mediaElement.SupportedPlaybackRates != null &&
                mediaElement.SupportedPlaybackRates.Count > 0)
            {
                mediaElement.SetPlaybackRate(.5);

                // make sure in play mode
                if (mediaElement.CurrentState == SmoothStreamingMediaElementState.Paused)
                    mediaElement.Play();
            }

            Logger.Log(new PlayerLog(PlayerLogType.SlowMotionClicked) { Sender = this, Message = "SlowMotionChecked" });
        }

        private void slowMotionElement_Unchecked(object sender, RoutedEventArgs e)
        {
            // don't process if set the checked state through code, not user interaction
            if (ignoreSlowMotionClick)
                return;

            if (mediaElement != null)
            {
                // We may be reacting to the toggle button being clicked, or another DVR button clicked.
                // If another DVR button was clicked, then we don't want to set the Play Speed or state.
                if (mediaElement.CurrentPlaybackState == PlaybackState.SlowMotionPlayback)
                {
                    mediaElement.Play();
                    mediaElement.SetPlaybackRate(1);
                }
            }

            Logger.Log(new PlayerLog(PlayerLogType.SlowMotionClicked) { Sender = this, Message = "SlowMotionUnchecked" });
        }

        private void chapterPreviousElement_Click(object sender, RoutedEventArgs e)
        {
            ControlHelper.RaiseEvent(PreviousChapterClicked, this);

            Logger.Log(new PlayerLog(PlayerLogType.PreviousChapterClicked) { Sender = this, Message = "PreviousChapterClicked" });
        }

        private void chapterNextElement_Click(object sender, RoutedEventArgs e)
        {
            ControlHelper.RaiseEvent(NextChapterClicked, this);
            Logger.Log(new PlayerLog(PlayerLogType.NextChapterClicked) { Sender = this, Message = "NextChapterClicked" });
        }

        private void fastForwardElement_Click(object sender, RoutedEventArgs e)
        {
            //Added check to make sure the SmoothStreamingMediaElement is in
            //a valid state before attempting to fast forward -Kevin Rohling 11/11/09 11:37am
            if (mediaElement != null &&
                !isSeeking &&
                (this.mediaElement.CurrentState == SmoothStreamingMediaElementState.Paused
                || this.mediaElement.CurrentState == SmoothStreamingMediaElementState.Playing
                || this.mediaElement.CurrentState == SmoothStreamingMediaElementState.Buffering))
            {

                if (this.UseSeekingBehavior)
                {
                    TimeSpan newPosition = this.mediaElement.Position.Add(this.SeekingInterval);
                    //Added check to only set isSeeking = true if using a SmoothStreamingSource
                    //this is because you will not get a SeekCompleted event when using Progressive Download.
                    //Kevin Rohling 1-13-2010 2:00PM
                    if (this.mediaElement.SmoothStreamingSource != null)
                    {
                        isSeeking = true;
                    }
                    UpdateVideoPosition(newPosition.TotalSeconds);
                }
                else
                {
                    // get the next fast forward value, loop back to 1.0
                    var list = from d in mediaElement.SupportedPlaybackRates
                               where d > mediaElement.PlaybackRate && d > 1.0
                               orderby d
                               select d;
                    double rate = (list.Count() == 0) ? 1.0 : list.First();
                    mediaElement.SetPlaybackRate(rate);
                }



                // make sure in play mode
                if (mediaElement.CurrentState == SmoothStreamingMediaElementState.Paused)
                    mediaElement.Play();

                Logger.Log(new PlayerLog(PlayerLogType.FastForwardClicked) { Sender = this, Message = "FastForwardClicked" });
            }
        }

        private void rewindElement_Click(object sender, RoutedEventArgs e)
        {
            //Added check to make sure the SmoothStreamingMediaElement is in
            //a valid state before attempting to rewind -Kevin Rohling 11/11/09 11:37am
            if (mediaElement != null &&
                (this.mediaElement.CurrentState == SmoothStreamingMediaElementState.Paused
                || this.mediaElement.CurrentState == SmoothStreamingMediaElementState.Playing
                || this.mediaElement.CurrentState == SmoothStreamingMediaElementState.Buffering))
            {
                if (this.UseSeekingBehavior)
                {
                    TimeSpan newPosition = this.mediaElement.Position.Subtract(this.SeekingInterval);
                    UpdateVideoPosition(newPosition.TotalSeconds);
                }
                else
                {
                    // get the next rewind value, loop back to 1.0
                    var list = from d in mediaElement.SupportedPlaybackRates
                               where d < mediaElement.PlaybackRate && d < -1.0
                               orderby d descending
                               select d;
                    double rate = (list.Count() == 0) ? 1.0 : list.First();
                    mediaElement.SetPlaybackRate(rate);
                }


                // make sure in play mode
                if (mediaElement.CurrentState == SmoothStreamingMediaElementState.Paused)
                    mediaElement.Play();

                Logger.Log(new PlayerLog(PlayerLogType.RewindClicked) { Sender = this, Message = "RewindClicked" });
            }
        }

        private void liveElement_Checked(object sender, RoutedEventArgs e)
        {
            // only jump to live if the user clicked the button, not set in our code
            if (!ignoreLiveElementClick)
            {
                // was unchecked (not live), not checked which is live
                JumpToLive();
            }
        }

        private void replayElement_Click(object sender, RoutedEventArgs e)
        {
            if (mediaElement != null)
            {
                // get new position, make sure is within range
                TimeSpan replayPosition = TimeSpan.FromSeconds(mediaElement.Position.TotalSeconds - ReplaySeconds);
                if (replayPosition < mediaElement.StartPosition)
                    replayPosition = mediaElement.StartPosition;

                // set new position	
                mediaElement.Position = replayPosition;

                // make sure in play mode when clicking instant replay
                if (mediaElement.CurrentState == SmoothStreamingMediaElementState.Paused)
                    mediaElement.Play();

                Logger.Log(new PlayerLog(PlayerLogType.ReplayClicked) { Sender = this, Message = "ReplayClicked" });
            }
        }

        // seek to the video live position, resume play, set to 1x playback speed
        private void JumpToLive()
        {
            if (mediaElement != null && mediaElement.IsLive)
            {
                // jump to live position
                if (mediaElement.StartSeekToLive() && this.JumpedToLive != null)
                {
                    this.JumpedToLive(this, new RoutedEventArgs());
                }

                // always want to start playing 
                if (mediaElement.CurrentState != SmoothStreamingMediaElementState.Playing)
                    mediaElement.Play();

                //Commented out the following block because it was causing the SSME
                //to enter a Retrying state. -Kevin Rohling 11/11/2009 4:07PM
                // reset playback speed
                //if (mediaElement.PlaybackRate != 1.0)
                //    mediaElement.SetPlaybackRate(1.0);

                // jump to live position
                mediaElement.StartSeekToLive();

            }

            Logger.Log(new PlayerLog(PlayerLogType.JumpToLiveClicked) { Sender = this, Message = "JumpToLiveClicked" });
        }

        private void CheckForReachedMarkers()
        {
            // make sure have marker data
            if (mediaElement == null || MarkerData == null)
            {
                return;
            }

            TimeSpan currentPosition = mediaElement.Position;
            // are we progressing forward on the timeline?
            bool movingForward = currentPosition > lastPollingPosition;
            TimeSpan start = movingForward ? lastPollingPosition : currentPosition;
            TimeSpan end = movingForward ? currentPosition : lastPollingPosition;

            // don't poll if we haven't moved at all
            if (start == end)
            {
                return;
            }

            TimeSpan actualSpan = end.Subtract(start);
            // not checking for low range right now
            // not worrying about < tick interval "seeks"
            //TimeSpan expectedLow = TimeSpan.FromMilliseconds(PollingUpdateInterval - PollingIntervalBuffer);
            TimeSpan expectedHigh = TimeSpan.FromMilliseconds(PollingUpdateInterval + PollingIntervalBuffer);

            if (actualSpan <= expectedHigh)
            {
                // natural (step)
                // steps can only be forward, so always handle inclusion with this assumption
                // this handles cases where the SSME can skip back slightly immediately after a seek
                // start <= time < end
                var reachedMarkers = (from marker in MarkerData.Markers
                                      where marker.Time >= start && marker.Time < end
                                      orderby marker.Time
                                      select marker);

                foreach (Marker marker in reachedMarkers)
                {
                    OnMarkerReached(marker);
                }
            }
            else
            {
                IEnumerable<Marker> skippedMarkers = null;

                // unnatural (seek)
                if (movingForward)
                {
                    // start <= time < end
                    // take skipped into into account
                    // we've only truely skipped if we passed the whole window of opportunity (time + duration)
                    skippedMarkers = (from marker in MarkerData.Markers
                                      where marker.Time.Add(marker.Duration) >= start &&
                                            marker.Time.Add(marker.Duration) < end
                                      orderby marker.Time
                                      select marker);
                }
                else
                {
                    // start < time <= end
                    skippedMarkers = (from marker in MarkerData.Markers
                                      where marker.Time > start && marker.Time <= end
                                      orderby marker.Time
                                      select marker);
                }

                if (skippedMarkers != null && skippedMarkers.Count() > 0)
                {
                    OnMarkersSkipped(skippedMarkers);
                }

                // check for markers we skipped into
                var skippedInto = (from marker in MarkerData.Markers
                                   where marker.Time < currentPosition &&
                                         marker.Time.Add(marker.Duration) > currentPosition
                                   orderby marker.Time
                                   select marker);

                foreach (Marker marker in skippedInto)
                {
                    OnMarkerSkippedInto(marker);
                }
            }

            lastPollingPosition = currentPosition;
        }

        private void OnMarkerReached(Marker marker)
        {
            // don't process event in Blend
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                return;
            }

            // raise event
            if (MarkerReached != null)
            {
                MarkerRoutedEventArgs args = new MarkerRoutedEventArgs();
                args.Marker = marker;
                MarkerReached(this, args);
            }

            Logger.Log(new PlayerLog(PlayerLogType.MarkerReached)
            {
                Sender = this,
                Message = string.Format(CultureInfo.InvariantCulture, "MarkerReached: {0}", marker.Time.ToString())
            });
        }

        private void OnMarkersSkipped(IEnumerable<Marker> markers)
        {
            // don't process event in Blend
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                return;
            }

            // raise event
            if (MarkersSkipped != null)
            {
                MarkerCollectionRoutedEventArgs args = new MarkerCollectionRoutedEventArgs();
                args.Markers = markers.ToList();
                MarkersSkipped(this, args);
            }

            Logger.Log(new PlayerLog(PlayerLogType.MarkersSkipped)
            {
                Sender = this,
                Message = string.Format(CultureInfo.InvariantCulture, "MarkersSkipped: {0}", markers.Count())
            });
        }

        private void OnMarkerSkippedInto(Marker marker)
        {
            // don't process event in Blend
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                return;
            }

            if (MarkerSkippedInto != null)
            {
                MarkerRoutedEventArgs args = new MarkerRoutedEventArgs();
                args.Marker = marker;
                MarkerSkippedInto(this, args);
            }

            Logger.Log(new PlayerLog(PlayerLogType.MarkerSkippedInto)
            {
                Sender = this,
                Message = string.Format(CultureInfo.InvariantCulture, "MarkerSkippedInto: {0}/{1}", marker.Time, marker.Duration)
            });
        }

        private static void OnLiveDurationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Player source = d as Player;
            source.OnLiveDurationChanged();
        }

        private static void OnScrubberRangeOverridePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Player source = d as Player;
            source.OnScrubberRangeOverrideChanged();
        }

        private static void OnAlwaysDisplayVODPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Player source = d as Player;
            source.OnAlwaysDisplayVODChanged();
        }

        private static void OnLiveDurationExtendPercentagePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Player source = d as Player;
            source.OnLiveDurationExtendPercentageChanged();
        }

        private static void OnHighDefinitionBitratePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Player source = d as Player;
            source.OnHighDefinitionBitrateChanged();
        }

        private static void OnMarkerDataPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Player source = d as Player;
            source.OnMarkerDataChanged();
        }

        private static void OnIsAdPlayingPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Player source = d as Player;

            if ((bool)e.NewValue)
            {
                VisualStateManager.GoToState(source, "AdPlaying", true);
            }
            else
            {
                VisualStateManager.GoToState(source, "AdCompleted", true);
            }

            source.OnIsAdPlayingChanged();
        }

        // the consumer can specify the duration of the live event,
        // this is used as the starting point for the estimated duration
        private void OnLiveDurationChanged()
        {
            if (mediaElement != null)
            {
                mediaElement.LiveDuration = this.LiveDuration;
            }
        }

        private void OnScrubberRangeOverrideChanged()
        {
            // update the scrubber bar to use the range overrides
            UpdatePositionScrubberRange();

            // update the time display
            UpdateCurrentPositionDisplay();
            UpdateTotalTimeDisplay();

            if (mediaElement != null)
            {
                // need to tell the ssme control the scrubber range, 
                // so it can make sure the position stays within range
                mediaElement.SetScrubberRange(ScrubberStartPosition, ScrubberEndPosition);
            }
        }

        private void OnAlwaysDisplayVODChanged()
        {
            UpdateLiveMode();
        }

        private void OnHighDefinitionBitrateChanged()
        {
            if (bitrateElement != null)
            {
                bitrateElement.HighDefinitionBitrate = HighDefinitionBitrate;
            }
        }

        private void OnLiveDurationExtendPercentageChanged()
        {
            if (mediaElement != null)
            {
                mediaElement.LiveDurationExtendPercentage = this.LiveDurationExtendPercentage;
            }
        }

        private void OnMarkerDataChanged()
        {
            if (positionElement != null)
            {
                // set data context to the marker data, required to layout the markers on the scrubber
                positionElement.DataContext = MarkerData;
                positionElement.UpdateMarkers();
            }
        }

        private void InitializeMediaElement()
        {
            if (mediaElement != null)
            {
                // set in control template
                if (mediaPresenterElement != null)
                    mediaPresenterElement.Content = mediaElement;

                // hookup new event handlers
                mediaElement.MediaOpened += mediaElement_MediaOpened;
                mediaElement.MediaFailed += mediaElement_MediaFailed;
                mediaElement.MediaEnded += mediaElement_MediaEnded;
                mediaElement.CurrentStateChanged += mediaElement_CurrentStateChanged;
                mediaElement.CurrentPlaybackStateChanged += mediaElement_CurrentPlaybackStateChanged;
                mediaElement.PlaybackBitrateChanged += mediaElement_PlaybackBitrateChanged;
                mediaElement.MaximumPlaybackBitrateChanged += mediaElement_MaximumPlaybackBitrateChanged;
                mediaElement.PlaybackRateChanged += mediaElement_PlaybackRateChanged;
                mediaElement.SeekCompleted += mediaElement_SeekCompleted;

                // instream data
                if (inStreamData != null)
                    inStreamData.MediaElement = mediaElement;

                // initialize
                OnLiveDurationChanged();
                OnLiveDurationExtendPercentageChanged();
                OnScrubberRangeOverrideChanged();
            }
        }


        private bool isSeeking = false;
        void mediaElement_SeekCompleted(object sender, SeekCompletedEventArgs e)
        {
            this.isSeeking = false;
        }

        private void UninitializeMediaElement()
        {
            if (mediaElement != null)
            {
                // unhook event handlers
                mediaElement.MediaOpened -= mediaElement_MediaOpened;
                mediaElement.MediaFailed -= mediaElement_MediaFailed;
                mediaElement.MediaEnded -= mediaElement_MediaEnded;
                mediaElement.CurrentStateChanged -= mediaElement_CurrentStateChanged;
                mediaElement.CurrentPlaybackStateChanged -= mediaElement_CurrentPlaybackStateChanged;
                mediaElement.PlaybackBitrateChanged -= mediaElement_PlaybackBitrateChanged;
                mediaElement.MaximumPlaybackBitrateChanged -= mediaElement_MaximumPlaybackBitrateChanged;
            }
        }

        public void ReplayVideo()
        {
            if (mediaElement != null)
            {
                // TODO: workaround for V1 of the player, better to do the following:
                //  mediaElement.Stop();
                //  mediaElement.Play();
                // but there is an issue with the SSME where the video hangs,
                // it's not in the buffering state and media-failed is not raised,
                // reload the video as a workaround

                Uri source = mediaElement.SmoothStreamingSource;
                mediaElement.SmoothStreamingSource = null;
                mediaElement.SmoothStreamingSource = new Uri(source.AbsoluteUri);
                mediaElement.Play();
            }
        }

        public void StartCountdown(TimeSpan duration, string displayFormat)
        {
            // eventually tie in display format

            if (countdownElement != null)
            {
                countdownElement.TextFormat = displayFormat;
                countdownElement.Duration = duration;
                countdownElement.Start();
            }
        }

        public void StopCountdown()
        {
            if (countdownElement != null)
            {
                countdownElement.Stop();
            }

            Logger.Log(new PlayerLog(PlayerLogType.AdCompleted) { Sender = this, Message = "AdCompleted" });
        }

        private void InitializeControls()
        {
            // disable all controls
            EnableControls(false);

            // update position text and scrubber
            UpdateDisplay();
        }

        private void EnableControls(bool enabled)
        {
            ControlHelper.EnableControl(playElement, enabled);
            ControlHelper.EnableControl(positionElement, enabled);
        }

        private void UpdateVideoPosition(double position)
        {
            if (mediaElement != null)
            {
                mediaElement.Position = TimeSpan.FromSeconds(position);
                UpdateDisplay();
            }
        }

        // update the UI to reflect the current player state
        private void UpdateDisplay()
        {
            // can't update anything if don't have a media element
            if (mediaElement == null)
            {
                return;
            }

            // see if currently scrubbing, only update the current position text
            if (positionElement != null && positionElement.IsDragging)
            {
                // update the position based on the seek-while-scrubbing flag,
                // either use the real video position, or the value of the scrubber
                if (SeekWhileScrubbing)
                    UpdateCurrentPositionDisplay();
                else
                    UpdateCurrentPositionDisplay(TimeSpan.FromSeconds(positionElement.Value));

                return;
            }

            // not scrubbering, update all display elements
            UpdateCurrentPositionDisplay();
            UpdateTotalTimeDisplay();
            UpdatePositionScrubber();
            UpdateLiveMode();
            UpdateRewindButton();
        }

        // update display when media ended, this updates the
        // current position and duration to the end time
        // of the video
        private void UpdateDisplayMediaEnded()
        {
            if (mediaElement != null)
            {
                // calculate the total duration
                TimeSpan time = IsOverrideScrubberRange ?
                    ScrubberEndPosition - ScrubberStartPosition :
                    mediaElement.EstimatedDuration - mediaElement.StartPosition;

                // curent position
                ControlHelper.SetTextBlockTime(currentTimeElement, time);

                // total time
                ControlHelper.SetTextBlockTime(totalTimeElement, time);

                if (positionElement != null)
                {
                    // scrubber
                    positionElement.Maximum = time.TotalSeconds;
                    positionElement.Available = time.TotalSeconds;
                    positionElement.Value = time.TotalSeconds;
                }
            }
        }

        private void UpdateCurrentPositionDisplay()
        {
            // update position using the real video position
            if (currentTimeElement != null && mediaElement != null && !mediaElement.IsSeeking)
                UpdateCurrentPositionDisplay(mediaElement.Position);
        }

        private void UpdateCurrentPositionDisplay(TimeSpan time)
        {
            if (currentTimeElement != null && mediaElement != null)
            {
                // times are in absolute time, but need to display in relative time
                TimeSpan displayTime = IsOverrideScrubberRange ?
                    time - ScrubberStartPosition :
                    time - mediaElement.StartPosition;

                // make sure within bounds
                if (displayTime < TimeSpan.Zero)
                    displayTime = TimeSpan.Zero;

                if (displayTime > mediaElement.EstimatedDuration)
                    displayTime = mediaElement.EstimatedDuration;

                ControlHelper.SetTextBlockTime(currentTimeElement, displayTime);
            }
        }

        private void UpdateTotalTimeDisplay()
        {
            if (totalTimeElement != null && mediaElement != null)
            {
                // calculate the total duration
                TimeSpan displayTime = IsOverrideScrubberRange ?
                    ScrubberEndPosition - ScrubberStartPosition :
                    mediaElement.EstimatedDuration - mediaElement.StartPosition;

                // make sure within range					
                if (displayTime < TimeSpan.Zero)
                    displayTime = TimeSpan.Zero;

                if (displayTime > mediaElement.EstimatedDuration)
                    displayTime = mediaElement.EstimatedDuration;

                ControlHelper.SetTextBlockTime(totalTimeElement, displayTime);
            }
        }

        private void UpdatePositionScrubber()
        {
            // first see if have elements, also make sure not currently seeking
            if (positionElement == null || mediaElement == null || mediaElement.IsSeeking)
                return;

            // update scrubber when playing video
            UpdatePositionScrubberRange();

            // uppdate available bar in live mode
            if (mediaElement != null && (LiveMode == LiveModeState.Live || LiveMode == LiveModeState.GoToLive))
            {
                // LivePosition is the position of the live feed, but not what is visible to the
                // user, need to back off that position by the buffer amount, otherwise the
                // available bar is always ahead of the scrubber thumb position
                double liveBuffer = (double)(mediaElement.LiveBufferSize / 1000);
                positionElement.Available = mediaElement.LivePosition - liveBuffer;
            }

            // update scrubber position in timeline
            positionElement.Value = mediaElement.Position.TotalSeconds;
        }

        // updates the min and max values of the scrubber
        private void UpdatePositionScrubberRange()
        {
            if (mediaElement != null && positionElement != null)
            {
                // determine if should use scrubber position overrides
                if (IsOverrideScrubberRange)
                {
                    // use overrides
                    positionElement.Minimum = ScrubberStartPosition.TotalSeconds;
                    positionElement.Maximum = ScrubberEndPosition.TotalSeconds;
                }
                else
                {
                    // don't use overrides, use values from the ssme control
                    positionElement.Minimum = mediaElement.StartPosition.TotalSeconds;
                    positionElement.Maximum = mediaElement.EstimatedDuration.TotalSeconds;
                }
            }
        }

        // set the live mode based on current video type (live or vod) and video position
        private void UpdateLiveMode()
        {
            // don't have a media element
            if (mediaElement == null)
            {
                LiveMode = LiveModeState.None;
                return;
            }

            // VOD mode
            if (!mediaElement.IsLive || AlwaysDisplayVOD)
            {
                LiveMode = LiveModeState.VOD;
                return;
            }

            // live mode has two states, Live means the position is currently at the live
            // position, GoToLive means it's a live video but the position is not at the 
            // live position, note there is a range that is considered to be at the live
            // position, otherwise it would always be in GoToLive mode
            LiveMode = mediaElement.IsLivePosition ? LiveModeState.Live : LiveModeState.GoToLive;
        }

        protected virtual void OnIsAdPlayingChanged()
        {
            if (IsAdPlayingChanged != null)
            {
                IsAdPlayingChanged(this, new RoutedEventArgs());
            }

            // make sure not displaying buffering indicator
            UpdateBufferingState(false);
        }

        // determine the buffering state based on the current state of the media element
        private void UpdateBufferingState()
        {
            if (mediaElement != null)
            {
                // make sure this feels right, might also check for MediaElementState.Opening
                bool buffering = (mediaElement.CurrentState == SmoothStreamingMediaElementState.Buffering);
                UpdateBufferingState(buffering);
            }
            else
            {
                // make sure buffering indicator is hidden if don't have a media element
                UpdateBufferingState(false);
            }
        }

        // set buffering state to the specified value
        private void UpdateBufferingState(bool buffering)
        {
            if (bufferingElement != null)
            {
                Visibility currentBufferingVisibility = bufferingElement.Visibility;

                bufferingElement.Visibility = buffering ? Visibility.Visible : Visibility.Collapsed;

                if (currentBufferingVisibility != bufferingElement.Visibility)
                {
                    Logger.Log(new PlayerLog(PlayerLogType.BufferingStatusChanged) { Sender = this, Message = "BufferingStatusChanged" });
                }
            }
        }

        private void CheckToEnableLogging()
        {
            try
            {
                // allow SSME tracing if specify on url command line
                if (!DesignerProperties.GetIsInDesignMode(this) && HtmlPage.Document.QueryString.ContainsKey("log"))
                {
                    // make sure did not specify to not log
                    string value = HtmlPage.Document.QueryString["log"].ToLower(CultureInfo.InvariantCulture);
                    if (value != "0" && value != "false")
                    {
                        // There are some issues around enabling tracing. If a TracingConfig.xml 
                        // file is in the application, logging can only be turned off by updating 
                        // the xml file. A workaround is to name the config xml file something 
                        // else besides TracingConfig.xml and use Tracing.ReadTraceConfig(xml file) 
                        // to read the file, but this does not work. Another workaround is to read 
                        // in the xml file manually, and call Tracing.ReadTraceConfig(xml string) 
                        // which works.
                        XElement xml = XElement.Load("SmoothStreamingTracingConfig.xml");
                        Tracing.ReadTraceConfig(xml.ToString());
                    }
                }
            }
            catch
            {
                // can get errors if the config xml file does not exist,
                // or parsing the xml, ignore any errors and continue
            }
        }
    }
}
