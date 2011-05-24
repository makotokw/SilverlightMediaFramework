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
using System.ComponentModel;
using Microsoft.SilverlightMediaFramework.Logging;
//using Microsoft.Web.Media.SmoothStreaming;
using System.IO;
using System.Windows.Data;

namespace Microsoft.SilverlightMediaFramework.Player
{
    public enum PlaybackState
    {
        Closed,
        Opening,
        Buffering,
        Playing,
        Paused,
        Stopped,
        Individualizing,
        AcquiringLicense,
        FastForwarding,
        Rewinding,
        SlowMotionPlayback,
        Scrubbing,
        Seeking
    }

    [TemplatePart(Name = "MediaElement", Type = typeof(MediaElement))]
    [TemplateVisualState(Name = "ClipPlayback", GroupName = "PlaybackModes")]
    [TemplatePart(Name = "RootElement", Type = typeof(Panel))]
    [TemplateVisualState(Name = "Normal", GroupName = "PlaybackModes")]
    public class CoreSmoothStreamingMediaElement : Control, IDisposable
    {           
        public static readonly DependencyProperty AttributesProperty;
        public static readonly DependencyProperty AudioStreamCountProperty;
        public static readonly DependencyProperty AudioStreamIndexProperty;
        public static readonly DependencyProperty AutoPlayProperty;
        public static readonly DependencyProperty BalanceProperty;
        public static readonly DependencyProperty BufferingProgressProperty;
        public static readonly DependencyProperty BufferingTimeProperty;
        public static readonly DependencyProperty CanPauseProperty;
        public static readonly DependencyProperty CanSeekProperty;
        public static readonly DependencyProperty CurrentStateProperty;
        public static readonly DependencyProperty DownloadProgressOffsetProperty;
        public static readonly DependencyProperty DownloadProgressProperty;
        public static readonly DependencyProperty IsMutedProperty;
        public static readonly DependencyProperty NaturalDurationProperty;
        public static readonly DependencyProperty NaturalVideoHeightProperty;
        public static readonly DependencyProperty NaturalVideoWidthProperty;
        public static readonly DependencyProperty PositionProperty;
        public static readonly DependencyProperty SourceProperty;
        public static readonly DependencyProperty StretchProperty;
        public static readonly DependencyProperty VolumeProperty;

        static CoreSmoothStreamingMediaElement()
        {
            AttributesProperty = DependencyProperty.Register("Attributes", typeof(Dictionary<string, string>), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(null));
            AudioStreamCountProperty = DependencyProperty.Register("AudioStreamCount", typeof(int), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(0));
            AudioStreamIndexProperty = DependencyProperty.Register("AudioStreamIndex", typeof(int?), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(null));
            AutoPlayProperty = DependencyProperty.Register("AutoPlay", typeof(bool), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(true));
            BalanceProperty = DependencyProperty.Register("Balance", typeof(double), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(0.0));
            BufferingProgressProperty = DependencyProperty.Register("BufferingProgress", typeof(double), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(0.0));
            BufferingTimeProperty = DependencyProperty.Register("BufferingTime", typeof(TimeSpan), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(null));
            CanPauseProperty = DependencyProperty.Register("CanPause", typeof(bool), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(true));
            CanSeekProperty = DependencyProperty.Register("CanSeek", typeof(bool), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(true));
            CurrentStateProperty = DependencyProperty.Register("CurrentState", typeof(MediaElementState), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(MediaElementState.Closed));
            DownloadProgressProperty = DependencyProperty.Register("DownloadProgress", typeof(double), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(0.0));
            DownloadProgressOffsetProperty = DependencyProperty.Register("DownloadProgressOffset", typeof(double), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(0.0));
            IsMutedProperty = DependencyProperty.Register("IsMuted", typeof(bool), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(false));
            VolumeProperty = DependencyProperty.Register("Volume", typeof(double), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(0.5));
            NaturalDurationProperty = DependencyProperty.Register("NaturalDuration", typeof(Duration), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(new Duration()));
            NaturalVideoWidthProperty = DependencyProperty.Register("NaturalVideoWidth", typeof(int), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(0));
            NaturalVideoHeightProperty = DependencyProperty.Register("NaturalVideoHeight", typeof(int), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(0));
            PositionProperty = DependencyProperty.Register("Position", typeof(TimeSpan), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(TimeSpan.Zero));
            SourceProperty = DependencyProperty.Register("Source", typeof(Uri), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(null, new PropertyChangedCallback(CoreSmoothStreamingMediaElement.OnSourcePropertyChanged)));
            StretchProperty = DependencyProperty.Register("Stretch", typeof(Stretch), typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata(Stretch.Uniform));
        }

        protected MediaElementData mediaElementData { get; set; } // UIができるまでの一時データ
        protected MediaElement mediaElement { get; set; }
        protected bool pendingPlay { get; set; }
        protected TimeSpan scrubberStartPosition { get; set; }
        protected TimeSpan scrubberEndPosition { get; set; }

        //public Dictionary<string, string> _Attributes { get { return this.mediaElement.Attributes; } }
        public int AudioStreamCount { get { return this.mediaElement.AudioStreamCount; } }
        public int? AudioStreamIndex { get { return this.mediaElement.AudioStreamIndex; } set { this.mediaElement.AudioStreamIndex = value; } }

        [Category("Media"), Description("AutoPlay of the media element.")]
        public bool AutoPlay
        {
            get { return (bool)GetValue(AutoPlayProperty); }
            set { SetValue(AutoPlayProperty, value); }
        }

        // TODO: 
        public double Balance { get { return this.mediaElement.Balance; } set { this.mediaElement.Balance = value; } }
        public double BufferingProgress { get { return this.mediaElement.BufferingProgress; } }
        public TimeSpan BufferingTime { get { return this.mediaElement.BufferingTime; } set { this.mediaElement.BufferingTime = value; } }

        [Category("Media"), Description("AutoPlay of the media element.")]
        public bool CanPause
        {
            get { return (bool)GetValue(CanPauseProperty); }
            set { SetValue(CanPauseProperty, value); }
        }

        [Category("Media"), Description("AutoPlay of the media element.")]
        public bool CanSeek
        {
            get { return (bool)GetValue(CanSeekProperty); }
            set { SetValue(CanSeekProperty, value); }
        }

        [Category("Media"), Description("CurrentState of the media element.")]
        public MediaElementState CurrentState
        {
            get { return (MediaElementState)GetValue(CurrentStateProperty); }
            internal set
            {
                SetValue(CurrentStateProperty, value);
                if (CurrentStateChanged != null)
                {
                    CurrentStateChanged(this, new RoutedEventArgs());
                }
            }
        }

        [Category("Media"), Description("DownloadProgress of the media element.")]
        public double DownloadProgress
        {
            get
            {
                double downloadProgress = 0.0;
                if (this.mediaElement != null)
                {
                    downloadProgress = this.mediaElement.DownloadProgress;
                }
                return downloadProgress;
            }
        }

        [Category("Media"), Description("DownloadProgressOffset of the media element.")]
        public double DownloadProgressOffset
        {
            get { return (double)base.GetValue(DownloadProgressOffsetProperty); }
            set { base.SetValue(DownloadProgressOffsetProperty, value); }
        }

        [Category("Media"), Description("DroppedFramesPerSecond of the media element.")]
        public double DroppedFramesPerSecond
        {
            get
            {
                double droppedFramesPerSecond = 0.0;
                if (this.mediaElement != null)
                {
                    droppedFramesPerSecond = this.mediaElement.DroppedFramesPerSecond;
                }
                return droppedFramesPerSecond;
            }
        }

        [Category("Media"), Description("IsMuted of the media element.")]
        public bool IsMuted
        {
            get { return (bool)GetValue(IsMutedProperty); }
            set { SetValue(IsMutedProperty, value); }
        }

        [Category("Media"), Description("Volume of the media element.")]
        public double Volume
        {
            get { return (double)GetValue(VolumeProperty); }
            set { SetValue(VolumeProperty, value); }
        }


        // shadow since throws exception if use when !mediaElementReady
        [Category("Media"), Description("Position of the media element.")]
        public TimeSpan Position
        {
            get
            {
                if (this.mediaElement != null)
                {
                    return mediaElement.Position;
                }
                return TimeSpan.Zero;
            }
            set
            {

                // when playing a clip, scrubberEndPosition and scrubberEndPosition are set,
                // make sure the specified position is within the clip range
                if (scrubberEndPosition != TimeSpan.Zero && scrubberEndPosition > scrubberStartPosition)
                {
                    value = GetPositionInRange(value, scrubberStartPosition, scrubberEndPosition);
                }
                else
                {
                    // make sure the position is within range of video
                    value = GetPositionInRange(value, StartPosition, EndPosition);
                }

                if (this.mediaElement != null &&
                            (this.mediaElement.CurrentState == MediaElementState.Playing ||
                                this.mediaElement.CurrentState == MediaElementState.Buffering ||
                                this.mediaElement.CurrentState == MediaElementState.Paused ||
                                this.mediaElement.CurrentState == MediaElementState.Stopped))
                {
                    this.mediaElement.Position = value;
                    base.SetValue(PositionProperty, value);
                }
            }
        }

        [Category("Media"), Description("Source of the media element.")]
        public Uri Source
        {
            get { return (Uri)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public Stretch Stretch
        {
            get { return (Stretch)GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }


        public bool EnableGPUAcceleration { get; set; }
        public TimeSpan EndPosition { get; protected set; }
        public TimeSpan EstimatedDuration { get { return this.EndPosition; } }

        public TimelineMarkerCollection Markers { get { return this.mediaElement.Markers; } }
        public Duration NaturalDuration { get { return this.mediaElement.NaturalDuration; } }
        public int NaturalVideoHeight { get { return this.mediaElement.NaturalVideoHeight; } }
        public int NaturalVideoWidth { get { return this.mediaElement.NaturalVideoWidth; } }
        public double PlaybackRate { get; set; }
        public double RenderedFramesPerSecond { get { return this.mediaElement.RenderedFramesPerSecond; } }
        public bool Scrubbing { get; set; }
        public TimeSpan StartPosition { get; set; }
        public IList<double> SupportedPlaybackRates { get; protected set; }

        //public long TotalBytesDownloaded { get; protected set; }

        public Dictionary<int, int> MediaElementStateToPlaybackStateMapping;        

        public LicenseAcquirer LicenseAcquirer
        {
            get { return (this.mediaElementData != null) ? this.mediaElementData.LicenseAcquirer : this.mediaElement.LicenseAcquirer; }
            set
            {
                if (this.mediaElementData != null)
                {
                    this.mediaElementData.LicenseAcquirer = value;
                }
                else if (this.mediaElement != null)
                {
                    this.mediaElement.LicenseAcquirer = value;
                }
            }
        }

        public event RoutedEventHandler BufferingProgressChanged;
        public event RoutedEventHandler CurrentStateChanged;
        public event RoutedEventHandler DownloadProgressChanged;
        //public event LogReadyRoutedEventHandler LogReady;
        //public event TimelineMarkerRoutedEventHandler MarkerReached;
        public event RoutedEventHandler MediaEnded;
        public event EventHandler<ExceptionRoutedEventArgs> MediaFailed;
        public event RoutedEventHandler MediaOpened;
        //public event EventHandler<TimelineEventArgs> TimelineEventReached;
        //public event RoutedEventHandler Retrying;
        //public event RoutedEventHandler RetrySuccessful;
        public event RoutedEventHandler CurrentPlaybackStateChanged;
        public event RoutedEventHandler PlaybackRateChanged;

        // TODO:  Hopefully we can just remove this whole section if the SSME has the planned states / events.
        //#region PlaybackState Temp Code

        //public void SetPlaybackRate(double rate)
        //{
        //    //    // can only set playback rate if playing a smooth stream video
        //    //    if (!IsSmoothStream)
        //    //        return;

        //    //    // make sure setting to a supported rate
        //    //    if (!SupportedPlaybackRates.Contains(rate))
        //    //        return;

        //    //    // setting the rate is an async call
        //    //    BeginSetPlaybackRate(rate);
        //}

        //// TODO: Remove the property when SSME ships with it (or something similar)
        //// Adding CurrentPlaybackState to simulate expected property in future code drop.
        //private PlaybackState currentPlaybackState = PlaybackState.Closed;
        //public PlaybackState CurrentPlaybackState
        //{
        //    get { return currentPlaybackState; }
        //    set
        //    {
        //        currentPlaybackState = value;
        //        ControlHelper.RaiseEvent(CurrentPlaybackStateChanged, this);
        //    }
        //}

        //private void SetCurrentPlaybackStateFromCurrentStateAndPlaybackRate()
        //{
        //    // Until MediaElementState & PlaybackState are merged, manually maintain CurrentPlaybackState

        //    double rate = PlaybackRate;

        //    if (CurrentState == MediaElementState.Playing)
        //    {
        //        if (rate == 1) // Normal Speed
        //        {
        //            CurrentPlaybackState = PlaybackState.Playing;
        //        }
        //        else if (rate < 0) // RW
        //        {
        //            CurrentPlaybackState = PlaybackState.Rewinding;
        //        }
        //        else if (rate > 1) // FF
        //        {
        //            CurrentPlaybackState = PlaybackState.FastForwarding;
        //        }
        //        else // Slow
        //        {
        //            CurrentPlaybackState = PlaybackState.SlowMotionPlayback;
        //        }
        //    }
        //    else if (MediaElementStateToPlaybackStateMapping.ContainsKey((int)CurrentState))
        //    {
        //        CurrentPlaybackState = (PlaybackState)MediaElementStateToPlaybackStateMapping[(int)CurrentState];
        //    }
        //    else
        //    {
        //        CurrentPlaybackState = PlaybackState.Playing;
        //    }
        //}

        //#endregion PlaybackState Temp Code

        public CoreSmoothStreamingMediaElement()
        {
            DefaultStyleKey = typeof(CoreSmoothStreamingMediaElement);

            this.PlaybackRate = 1.0;
            this.scrubberStartPosition = TimeSpan.Zero;
            this.scrubberEndPosition = TimeSpan.Zero;
            this.StartPosition = TimeSpan.Zero;
            this.EndPosition = TimeSpan.Zero;

            this.mediaElementData = new MediaElementData();

            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                LoadStateMappingDictionary();
            }
        }


        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                this.InitializeMediaElement(this.GetTemplateChild("MediaElement") as MediaElement);
            }
        }

        void InitializeMediaElement(MediaElement me)
        {
            if (this.mediaElement != me)
            {
                if (this.mediaElementData != null && me != null)
                {
                    var cache = this.mediaElementData;
                    this.mediaElementData = null;
                    cache.Dettach(me);
                }
                this.mediaElement = me;

                if (this.mediaElement != null)
                {
                    this.mediaElement.BufferingProgressChanged += new RoutedEventHandler(mediaElement_BufferingProgressChanged);
                    this.mediaElement.CurrentStateChanged += new RoutedEventHandler(mediaElement_CurrentStateChanged);
                    this.mediaElement.DownloadProgressChanged += new RoutedEventHandler(mediaElement_DownloadProgressChanged);
                    //this.mediaElement.LogReady += new LogReadyRoutedEventHandler(mediaElement_LogReady);
                    //this.mediaElement.MarkerReached += new TimelineMarkerRoutedEventHandler(mediaElement_MarkerReached);
                    this.mediaElement.MediaOpened += new RoutedEventHandler(mediaElement_MediaOpened);
                    this.mediaElement.MediaFailed += new EventHandler<ExceptionRoutedEventArgs>(mediaElement_MediaFailed);
                    this.mediaElement.MediaEnded += new RoutedEventHandler(mediaElement_MediaEnded);
                }
            }
        }

        //void mediaElement_MarkerReached(object sender, TimelineMarkerRoutedEventArgs e)
        //{
            
        //}

        //void mediaElement_LogReady(object sender, LogReadyRoutedEventArgs e)
        //{
            
        //}

        void mediaElement_BufferingProgressChanged(object sender, RoutedEventArgs e)
        {
            if (BufferingProgressChanged != null)
            {
                BufferingProgressChanged(this, new RoutedEventArgs());
            }
        }

        void mediaElement_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            var mediaElement = sender as MediaElement;
            var currentState = this.CurrentState;
            if (mediaElement != null && this.CurrentState != mediaElement.CurrentState)
            {
                base.SetValue(CurrentStateProperty, mediaElement.CurrentState);
            }
            var handler = this.CurrentStateChanged;
            if ((handler != null) && (currentState != this.CurrentState))
            {
                handler(this, new RoutedEventArgs());
            }
            //SetCurrentPlaybackStateFromCurrentStateAndPlaybackRate();
        }

        void mediaElement_DownloadProgressChanged(object sender, RoutedEventArgs e)
        {
            if (DownloadProgressChanged != null)
            {
                DownloadProgressChanged(this, new RoutedEventArgs());
            }
        }

        void mediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            this.StartPosition = TimeSpan.Zero;
            this.EndPosition = this.mediaElement.NaturalDuration.TimeSpan;

            // see if consumer called Play after specfiying the source but before the video was loaded
            if (pendingPlay)
            {
                Play();
                pendingPlay = false;
            }

            if (MediaOpened != null)
            {
                MediaOpened(this, e);
            }
        }

        void mediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (MediaEnded != null)
            {
                MediaEnded(this, e);
            }
        }

        void mediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Logger.Log(new PlayerLog(PlayerLogType.MediaFailed) { Sender = this, Message = string.Format("MediaFailed: {0}", e.ErrorException.Message) });

            if (MediaFailed != null)
            {
                MediaFailed(this, e);
            }
        }

        public virtual void Play()
        {
            // workaround for SSME issue, the SSME can have odd behavior when calling Play 
            // from the SeekComplete event handle when it's currently in the Playing state
            if (CurrentState == MediaElementState.Playing)
            {
                return;
            }

            // set flag, in case video has not been loaded yet
            pendingPlay = true;

            // not seeking, go ahead and call Play
            Logger.Log(new PlayerLog(PlayerLogType.PlayVideo) { Sender = this, Message = "Play" });
            if (this.mediaElement != null)
            {
                this.mediaElement.Play();
            }
            
        }

        public virtual void Pause()
        {
            if (this.mediaElement != null)
            {
                this.mediaElement.Pause();
            }
        }

        public void SetSource(Stream stream)
        {
            if (this.mediaElementData != null)
            {
                this.mediaElementData.Stream = stream;
            }
            else if (this.mediaElement != null)
            {
                this.mediaElement.SetSource(stream);
            }
        }

        public virtual void Stop()
        {
            if (this.mediaElement != null)
            {
                this.mediaElement.Stop();
            }
        }

        protected virtual void UpdateVisualState(bool useTransitions)
        {

        }

        // update any pending properties
        private void ApplyProperties()
        {
            // update properties that might have been set
            // before the media-ready event was raised
            OnSourceChanged();
        }

        #region dependency property callbacks

        private static void OnSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            CoreSmoothStreamingMediaElement source = d as CoreSmoothStreamingMediaElement;
            source.OnSourceChanged();
        }

        private void OnSourceChanged()
        {
            if (Source != null)
            {
                // clear flag, set in the Play method
                pendingPlay = false;

                // reset duration, updated when video is opened
                this.EndPosition = TimeSpan.Zero;

                // load a progressive download video
                if (this.mediaElement != null)
                {
                    this.mediaElement.Source = this.Source;
                }
                else if (this.mediaElementData != null)
                {
                    this.mediaElementData.Source = this.Source;
                }
            }
            Logger.Log(new PlayerLog(PlayerLogType.SourceChanged) { Sender = this, Message = string.Format("SourceChanged: {0}", Source) });
        }

        #endregion

        // can specify the range when playing a clip, this is used when setting the
        // position to make sure the position is within the clip range, this is a method
        // instead of properties to make it clear that these are not bindable properties
        // and are only settable
        internal void SetScrubberRange(TimeSpan scrubberStartPosition, TimeSpan scrubberEndPosition)
        {
            this.scrubberStartPosition = scrubberStartPosition;
            this.scrubberEndPosition = scrubberEndPosition;
        }

        private void LoadStateMappingDictionary()
        {
            MediaElementStateToPlaybackStateMapping = new System.Collections.Generic.Dictionary<int, int>();

            // PlaybackState is a "superset" of MediaElementState
            for (int i = 0; i <= (int)MediaElementState.Stopped; i++)
            {
                MediaElementState meState = (MediaElementState)i;
                PlaybackState pbState = (PlaybackState)Enum.Parse(typeof(PlaybackState), meState.ToString(), true);
                MediaElementStateToPlaybackStateMapping.Add((int)meState, (int)pbState);
            }
        }

        // make sure the position is in the range of the min and max positions
        private TimeSpan GetPositionInRange(TimeSpan position, TimeSpan minPosition, TimeSpan maxPosition)
        {
            if (position < minPosition)
                position = minPosition;

            if (position > maxPosition)
                position = maxPosition;

            return position;
        }

        public void Dispose()
        {
        }

        protected class MediaElementData
        {
            public LicenseAcquirer LicenseAcquirer;
            public List<TimelineMarker> Markers = new List<TimelineMarker>();
            public Stream Stream;
            public Uri Source;

            public void Dettach(MediaElement target)
            {
                if (this.LicenseAcquirer != null)
                {
                    target.LicenseAcquirer = this.LicenseAcquirer;
                    this.LicenseAcquirer = null;
                }
                if (this.Markers.Count > 0)
                {
                    foreach (var marker in this.Markers)
                    {
                        target.Markers.Add(marker);
                    }
                    this.Markers.Clear();
                }
                if (this.Stream != null)
                {
                    target.SetSource(this.Stream);
                    this.Stream = null;
                }
                else if (this.Source != null)
                {
                    target.Source = this.Source;
                }
            }
        }

    }

 

}
