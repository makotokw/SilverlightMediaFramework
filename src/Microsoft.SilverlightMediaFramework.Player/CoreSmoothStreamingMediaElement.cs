using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Media;
using Microsoft.SilverlightMediaFramework.Logging;
using Microsoft.Web.Media.SmoothStreaming;

namespace Microsoft.SilverlightMediaFramework.Player
{
	// Shim class that the consumer uses instead of SmoothStreamingMediaElement directly.
	public class CoreSmoothStreamingMediaElement : SmoothStreamingMediaElement
	{
		public new event RoutedEventHandler MediaOpened;
		public new event EventHandler<ExceptionRoutedEventArgs> MediaFailed;
        public new event RoutedEventHandler MediaEnded;
		public event RoutedEventHandler Retrying;
		public event RoutedEventHandler RetrySuccessful;
		public event RoutedEventHandler CurrentPlaybackStateChanged;
		public event EventHandler<SimpleEventArgs<bool>> TrickPlayStateChanged;
		public event RoutedEventHandler MaximumPlaybackBitrateChanged;
		public event RoutedEventHandler PlaybackBitrateChanged;
		public static event EventHandler CookiesChanged;
		public event RoutedEventHandler PlaybackRateChanged;
		
		public Dictionary<int, int> MediaElementStateToPlaybackStateMapping;

		// the duration for live and vod modes
		private TimeSpan estimatedDuration;

		// flag if consumer of the control called Play before the video was loaded
		private bool pendingPlay;

		// stores the command to execute when seek completes,
		// allocate here (not ctor), to make sure works in Blend
		private SeekCommand seekCommand = new SeekCommand();

		// used when setting clip range
		private TimeSpan scrubberStartPosition;
		private TimeSpan scrubberEndPosition;

		// stores the state of the ssme player, so can be restored
		private RetryMonitor retryMonitor;

		// stores the max bitrate for the streams
		private ulong maxSelectedStreamBitrate;

		// Used to store the token cookie
		public static CookieContainer cookies;

        private bool isLivePosition;
		
		public RetryState RetryState
		{
			get
			{
				return retryMonitor.RetryState;
			}
		}

		public int RetryAttempt
		{
			get
			{
				return retryMonitor.RetryAttempt;
			}
		}

		public bool IsMiniCam
		{
			get { return (bool)GetValue(IsMiniCamProperty); }
			set { SetValue(IsMiniCamProperty, value); }
		}

		public static readonly DependencyProperty IsMiniCamProperty =
			DependencyProperty.Register("IsMiniCam", typeof(bool), typeof(CoreSmoothStreamingMediaElement), 
			new PropertyMetadata(false));

		[Category("Media"), Description("Interval between auto retry attempts.")]
		public TimeSpan RetryInterval
		{
			get { return (TimeSpan)GetValue(RetryIntervalProperty); }
			set { SetValue(RetryIntervalProperty, value); }
		}

		public static readonly DependencyProperty RetryIntervalProperty =
			DependencyProperty.Register("RetryInterval", typeof(TimeSpan),
			typeof(CoreSmoothStreamingMediaElement),
			new PropertyMetadata(CoreSmoothStreamingMediaElement.OnRetryIntervalPropertyChanged));

		[Category("Media"), Description("Duration to try auto retries.")]
		public TimeSpan RetryDuration
		{
			get { return (TimeSpan)GetValue(RetryDurationProperty); }
			set { SetValue(RetryDurationProperty, value); }
		}

		public static readonly DependencyProperty RetryDurationProperty =
			DependencyProperty.Register("RetryDuration", typeof(TimeSpan),
			typeof(CoreSmoothStreamingMediaElement),
			new PropertyMetadata(CoreSmoothStreamingMediaElement.OnRetryDurationPropertyChanged));

		[Category("Media"), Description("Source of the media element.")]
		public new Uri Source
		{
			get { return (Uri)GetValue(SourceProperty); }
			set { SetValue(SourceProperty, value); }
		}

		public static new readonly DependencyProperty SourceProperty =
			DependencyProperty.Register("Source", typeof(Uri),
			typeof(CoreSmoothStreamingMediaElement),
			new PropertyMetadata(CoreSmoothStreamingMediaElement.OnSourcePropertyChanged));

		[Category("Media"), Description("Smooth streaming source of the media element.")]
		public new Uri SmoothStreamingSource
		{
			get { return (Uri)GetValue(SmoothStreamingSourceProperty); }
			set { SetValue(SmoothStreamingSourceProperty, value); }
		}

		public static new readonly DependencyProperty SmoothStreamingSourceProperty =
			DependencyProperty.Register("SmoothStreamingSource", typeof(Uri),
			typeof(CoreSmoothStreamingMediaElement),
			new PropertyMetadata(CoreSmoothStreamingMediaElement.OnSmoothStreamingSourcePropertyChanged));

		[Category("Media"), Description("Get last maximum playback bitrate.")]
		public ulong MaximumPlaybackBitrate
		{
			get { return (ulong)GetValue(MaximumPlaybackBitrateProperty); }
			private set { SetValue(MaximumPlaybackBitrateProperty, value); }
		}

		public static readonly DependencyProperty MaximumPlaybackBitrateProperty =
			DependencyProperty.Register("MaximumPlaybackBitrate", typeof(ulong),
			typeof(CoreSmoothStreamingMediaElement), null);

		[Category("Media"), Description("Get last playback bitrate.")]
		public ulong PlaybackBitrate
		{
			get { return (ulong)GetValue(PlaybackBitrateProperty); }
			private set { SetValue(PlaybackBitrateProperty, value); }
		}

		public static readonly DependencyProperty PlaybackBitrateProperty =
			DependencyProperty.Register("PlaybackBitrate", typeof(ulong),
			typeof(CoreSmoothStreamingMediaElement), null);

		[Category("Media"), Description("Get last download bitrate.")]
		public ulong DownloadBitrate
		{
			get { return (ulong)GetValue(DownloadBitrateProperty); }
			private set { SetValue(DownloadBitrateProperty, value); }
		}

		public static readonly DependencyProperty DownloadBitrateProperty =
			DependencyProperty.Register("DownloadBitrate", typeof(ulong),
			typeof(CoreSmoothStreamingMediaElement), null);

		public TimeSpan LivePositionRange
		{
			get { return (TimeSpan)GetValue(LivePositionRangeProperty); }
			set { SetValue(LivePositionRangeProperty, value); }
		}

		public static readonly DependencyProperty LivePositionRangeProperty =
			DependencyProperty.Register("LivePositionRange", typeof(TimeSpan), 
			typeof(CoreSmoothStreamingMediaElement), null);

		// this is only used since the SSME PlaybackRate is not a dependency property,
		// but we want to bind to the values, create a mirrored value that can be
		// databound as a workaround
		public double PlaybackRateDisplay
		{
			get { return (double)GetValue(PlaybackRateDisplayProperty); }
			set { SetValue(PlaybackRateDisplayProperty, value); }
		}

		public static readonly DependencyProperty PlaybackRateDisplayProperty =
			DependencyProperty.Register("PlaybackRateDisplay", typeof(double),
			typeof(CoreSmoothStreamingMediaElement), new PropertyMetadata((double)1.0));

		// TODO: workaround, SSME version 596 is not implementing IsLivePosition, it's 
		// only set when StartSeekToLive is called, and cleared whenever a seek occurs. 
		// This workaround return true when playing a live stream and the position is 
		// within range of LivePosition.
        public new bool IsLivePosition
        {
            get
            {
                if (!IsLive)
                {
                    // always return false for non live streams
                    return false;
                }

                // determine if the position is outside of the live position range,
                // to prevent frequent changes, check to see if the position is within 
                // a range and return the current value
                double lowerRange = LivePosition - (LivePositionRange.TotalSeconds * 1.1);
                double upperRange = LivePosition - (LivePositionRange.TotalSeconds * 0.9);
                if (Position.TotalSeconds >= lowerRange && Position.TotalSeconds <= upperRange)
                {
                    // the position is within the buffer range, return current value
                    return isLivePosition;
                }

                // outside of the buffer range, determine if live position
                isLivePosition = Position.TotalSeconds > upperRange;
                return isLivePosition;
            }
        }


		public static CookieContainer Cookies
		{
			get
			{
				if (cookies == null)
				{
					cookies = new CookieContainer();
				}
				return cookies;
			}
			set
			{
				cookies = value;
				OnCookiesChanged(new EventArgs());
			}
		}

		private static void OnCookiesChanged(EventArgs e)
		{
			if (CookiesChanged != null)
				CookiesChanged(null, e);
		}

		public bool IsSmoothStream
		{
			get
			{
				return SmoothStreamingSource != null;
			}
		}

		// The duration of the video for live and vod streams. The SSME control contains
		// a Duration property that is EndPosition - StartPosition. Instead of overriding 
		// the behavior of Duration, create another property that contains the total 
		// duration for vod and live feeds.
		public TimeSpan EstimatedDuration
		{
			get
			{
				// for vod streams, the duration is the EndPosition
				if (!IsLive)
				{
					return EndPosition;
				}

				// detect if exceeded the duration
				if (LivePosition > estimatedDuration.TotalSeconds)
				{
					// increase the estimated duration by the time represented by the timeline, 
					// example if StartPosition = 100 seconds, and LivePosition = 300 seconds,
					// increase the time based on 200 seconds, not 300 seconds
					double range = LivePosition - StartPosition.TotalSeconds;

                    //SSME Workaround: LivePosition is being reported inaccurately
                    //causing an overflow exception to occur in the TimeSpan.FromSeconds
                    //method.  To prevent this exception from halting the application
                    //the following try-catch block was added to suppress it.
                    //Kevin Rohling 11-10-09 1:38PM
                    try
                    {
                        estimatedDuration = TimeSpan.FromSeconds(
                            LivePosition + (range * LiveDurationExtendPercentage));
                    }
                    catch (OverflowException) { }
				}

				return estimatedDuration;
			}
		}

		// if the ssme control is currently seeking
		public bool IsSeeking
		{
			get 
			{ 
				return seekCommand.IsSeeking; 
			}
		}		

		// shadow since throws exception if use when !mediaElementReady
		public new TimeSpan Position
		{
			get
			{
				return base.Position;
			}

			set
			{
				// if currently seeking, store the desired new position
				// and seek to it when the current seek operation completes
				if (seekCommand.IsSeeking)
				{
					seekCommand.Position = value;
					return;
				}

				// make sure the specified position is within range, this requires a workaround
				// since the SSME control does not update LivePosition and EndPosition when in 
				// PIP mode, LivePosition and EndPosition appear to be set when the video is
				// loaded but the properties are not updated, the workaround is to not check 
				// if the position is within range when in PIP mode
				if (!PipMode)
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
						TimeSpan maxPosition = IsLive ? TimeSpan.FromSeconds(LivePosition) : EndPosition;
						value = GetPositionInRange(value, StartPosition, maxPosition);
					}
				}
				
				// workaround, handle case when set position and CurrentState = Paused, 
				// if set IsSeeking = true a SeekCompleted event is never raised, so the
				// SSME control will ignore future Position and Play commands

                //Changed this to only set IsSeeking = true if using SmoothStreaming.  This is
                //because you will not get a SeekCompleted event when using Progressive Download.
                //Kevin Rohling 1-13-2010 1:59PM
                if (CurrentState == SmoothStreamingMediaElementState.Playing && this.SmoothStreamingSource != null)
				{
					// set flag that we are seeking, cleared in the SeekCompleted event handler
					seekCommand.IsSeeking = true;
				}

				base.Position = value;
			}
		}


		// TODO:  Hopefully we can just remove this whole section if the SSME has the planned states / events.
		#region PlaybackState Temp Code

		// the estimated duration of the live video
		public TimeSpan LiveDuration { get; set; }

		// amount to increase the duration when exceed the live video duration
		public double LiveDurationExtendPercentage { get; set; }

		public new void SetPlaybackRate(double rate)
		{
			// can only set playback rate if playing a smooth stream video
			if (!IsSmoothStream)
				return;

			// make sure setting to a supported rate
			if (!SupportedPlaybackRates.Contains(rate))
				return;

			// setting the rate is an async call
			BeginSetPlaybackRate(rate);
		}

		private void BeginSetPlaybackRate(double rate)
		{
			// check if currently seeking, if so, queue the desired rate and it will 
			// be set after the current seek completes, note that the last-in playback 
			// rate will be used (if try to set multiple rates while seeking, the last
			// one will be used)		
			if (seekCommand.IsSeeking)
			{
				seekCommand.PlaybackRate = rate;
				return;
			}
			
			// not seeking, set the rate, but it's not really set until the seek command completes
			if (rate != PlaybackRate)
			{
				seekCommand.IsSeeking = true;
				base.SetPlaybackRate(rate);
			}
		}

		// TODO: Remove the property when SSME ships with it (or something similar)
		// Adding CurrentPlaybackState to simulate expected property in future code drop.
		private PlaybackState currentPlaybackState = PlaybackState.Closed;
		public PlaybackState CurrentPlaybackState
		{
			get
			{
				return currentPlaybackState;
			}

			set
			{
				currentPlaybackState = value;
				ControlHelper.RaiseEvent(CurrentPlaybackStateChanged, this);
			}
		}

		private void CoreSmoothStreamingMediaElement_CurrentStateChanged(object sender, RoutedEventArgs e)
		{
			SetCurrentPlaybackStateFromCurrentStateAndPlaybackRate();
		}

		private void SetCurrentPlaybackStateFromCurrentStateAndPlaybackRate()
		{
			// Until MediaElementState & PlaybackState are merged, manually maintain CurrentPlaybackState

			double rate = PlaybackRate;

            if (CurrentState == SmoothStreamingMediaElementState.Playing)
			{
				if (rate == 1) // Normal Speed
				{
					CurrentPlaybackState = PlaybackState.Playing;
				}
				else if (rate < 0) // RW
				{
					CurrentPlaybackState = PlaybackState.Rewinding;
				}
				else if (rate > 1) // FF
				{
					CurrentPlaybackState = PlaybackState.FastForwarding;
				}
				else // Slow
				{
					CurrentPlaybackState = PlaybackState.SlowMotionPlayback;
				}
			}
			else if (MediaElementStateToPlaybackStateMapping.ContainsKey((int)CurrentState))
			{
				CurrentPlaybackState = (PlaybackState)MediaElementStateToPlaybackStateMapping[(int)CurrentState];
			}
			else
			{
				CurrentPlaybackState = PlaybackState.Playing;
			}
		}

		#endregion PlaybackState Temp Code

		public CoreSmoothStreamingMediaElement()
		{
			if (!DesignerProperties.GetIsInDesignMode(this))
			{
				CoreSmoothStreamingMediaElement.CookiesChanged += CoreSmoothStreamingMediaElement_CookiesChanged;
				base.MediaOpened += CoreSmoothStreamingMediaElement_MediaOpened;
                base.MediaEnded += CoreSmoothStreamingMediaElement_MediaEnded;
				PlaybackTrackChanged += CoreSmoothStreamingMediaElement_PlaybackTrackChanged;
				DownloadTrackChanged += CoreSmoothStreamingMediaElement_DownloadTrackChanged;
				CurrentStateChanged += CoreSmoothStreamingMediaElement_CurrentStateChanged;
				SeekCompleted += CoreSmoothStreamingMediaElement_SeekCompleted;
				base.MediaFailed += CoreSmoothStreamingMediaElement_MediaFailed;
				SmoothStreamingErrorOccurred += CoreSmoothStreamingMediaElement_SmoothStreamingErrorOccurred;

				// TODO: workaround since not exposing the max playback bitrate
				Application.Current.Host.Content.FullScreenChanged += Application_FullScreenChanged;

				// retry helper
				retryMonitor = new RetryMonitor(this);
				retryMonitor.Retrying += retryMonitor_Retrying;
				retryMonitor.RetrySuccessful += retryMonitor_RetrySuccessful;
				retryMonitor.RetryFailed += retryMonitor_RetryFailed;
				retryMonitor.RetryAttempted += retryMonitor_RetryAttempted;
				
				LoadStateMappingDictionary();
			}
		}

        private void CoreSmoothStreamingMediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (MediaEnded != null)
            {
                MediaEnded(sender, e);
            }
        }


		private void retryMonitor_RetryAttempted(object sender, RoutedEventArgs e)
		{
			// log the retry attempt
			Logger.Log(new PlayerLog(PlayerLogType.RetryAttempt, RetryAttempt) { Sender = this, Message = string.Format("RetryAttempt: {0}", RetryAttempt) });
		}

		private void retryMonitor_Retrying(object sender, SimpleEventArgs<Exception> e)
		{
			// log error
			if (e != null && e.Result != null && e.Result.Message != null)
				Logger.Log(new PlayerLog(PlayerLogType.MediaFailedRetry) { Sender = this, Message = string.Format("MediaRetry:{0}", e.Result.Message) });
			else
				Logger.Log(new PlayerLog(PlayerLogType.MediaFailedRetry) { Sender = this, Message = string.Format("MediaRetry: (no exception)") });

			// pass to any consumers
			if (Retrying != null)
			{
				Retrying(this, null);
			}
		}

		private void retryMonitor_RetrySuccessful(object sender, RoutedEventArgs e)
		{
			// log event
			Logger.Log(new PlayerLog(PlayerLogType.MediaRetrySucceeded) { Sender = this, Message = "MediaRetrySucceeded" });
		
			// pass to any consumers
			if (RetrySuccessful != null)
			{
				RetrySuccessful(this, e);
			}
		}

		private void retryMonitor_RetryFailed(object sender, ExceptionRoutedEventArgs e)
		{
			// log error
			Logger.Log(new PlayerLog(PlayerLogType.MediaDied) { Sender = this, Message = "MediaDied" });
			
			// pass to any consumers, the arg e contains the original MediaFailed arg
			if (MediaFailed != null)
			{
				MediaFailed(this, e);
			}
		}

		// retry loading the control with the last known good values,
		// retries one time, does not perform auto retry
		public void Retry()
		{
			retryMonitor.Retry();
		}


		public void Retry(TimeSpan timeSpan)
		{
			retryMonitor.Retry(timeSpan);
		}

		// stop and reset the auto retry
		public void ResetAutoRetry()
		{
			// TODO: better fix for Blend.
			if (!DesignerProperties.GetIsInDesignMode(this))
			{
				retryMonitor.ResetAutoRetry();
			}
		}

		void CoreSmoothStreamingMediaElement_CookiesChanged(object sender, EventArgs e)
		{
			if (!IsMiniCam)
			{
				base.CookieContainer = CoreSmoothStreamingMediaElement.Cookies;
			}
		}

		public override void OnApplyTemplate()
		{
			if (!DesignerProperties.GetIsInDesignMode(this))
			{
				// the SmoothStreamingMediaElement contains an underlying media 
				// element, and certain operations cannot be performed until
				// the media element is initialized
				if (!IsMiniCam)
				{
					base.CookieContainer = CoreSmoothStreamingMediaElement.Cookies;
				}
			
				base.OnApplyTemplate();
			}
		}

		private void CoreSmoothStreamingMediaElement_PlaybackTrackChanged(object sender, TrackChangedEventArgs e)
		{
			if (e.StreamType == MediaStreamType.Video && PlaybackBitrate != e.Track.Bitrate)
			{
				// store value
				PlaybackBitrate = e.Track.Bitrate;
				
				// raise event
				if (PlaybackBitrateChanged != null)
				{
					PlaybackBitrateChanged(this, new RoutedEventArgs());
				}
			}
		}

		private void CoreSmoothStreamingMediaElement_DownloadTrackChanged(object sender, TrackChangedEventArgs e)
		{
			if (e.StreamType == MediaStreamType.Video)
			{
				// store the current bitrate
				DownloadBitrate = e.Track.Bitrate;
			}
		}

		private void CoreSmoothStreamingMediaElement_MediaOpened(object sender, RoutedEventArgs e)
		{
            isLivePosition = false;

			// opened a new stream, clear any existing seek states
			seekCommand.IsSeeking = false;
			
			// initialize the duration to the estimated time specified in the manifest, the
			// LiveDuration is relative time, so add StartPosition to make it absoulte time
			if (IsLive)
			{
				estimatedDuration = StartPosition + LiveDuration;
			}

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

			// get the max bitrate for this stream
			UpdateSelectedStreamsMaximumBitrate();
		}

		private void CoreSmoothStreamingMediaElement_SmoothStreamingErrorOccurred(object sender, SmoothStreamingErrorEventArgs e)
		{
			// log error
			string message = String.Format("SmoothStreamingErrorOccurred - code: {0}, source: {1}, message: {2}", 
				e.ErrorCode, SmoothStreamingSource.AbsoluteUri, e.ErrorMessage);
			Logger.Log(new PlayerLog(PlayerLogType.SmoothStreamingError) { Sender = this, Message = message });
		}

		private void CoreSmoothStreamingMediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
		{
			Logger.Log(new PlayerLog(PlayerLogType.MediaFailed) { Sender = this, Message = string.Format("MediaFailed: {0}", e.ErrorException.Message) });

			// opened a new stream, clear any existing seek states
			seekCommand.IsSeeking = false;
		}

		public override void Play()
		{
			// workaround for SSME issue, the SSME can have odd behavior when calling Play 
			// from the SeekComplete event handle when it's currently in the Playing state
            if (CurrentState == SmoothStreamingMediaElementState.Playing)
			{
				return;
			}
				
			// set flag, in case video has not been loaded yet
			pendingPlay = true;
			
			// the SSME control has a problem if call Play when it's currently 
			// seeking, set a flag so can call Play when seeking completes
			if (seekCommand.IsSeeking)
			{
				seekCommand.Play = true;
			}
			else
			{
				// not seeking, go ahead and call Play
				Logger.Log(new PlayerLog(PlayerLogType.PlayVideo) { Sender = this, Message = "Play" });
				base.Play();
			}
		}

		// update any pending properties
		private void ApplyProperties()
		{
			// update properties that might have been set
			// before the media-ready event was raised
			OnSourceChanged();
			OnSmoothStreamingSourceChanged();
		}

		private void CoreSmoothStreamingMediaElement_SeekCompleted(object sender, SeekCompletedEventArgs e)
		{
			OnSeekCompleted(e.ActualSeekPosition);
            CheckEndOfVideo();
		}

        // SSME (and MediaElement) do not raise the MediaEnded event when scrub 
        // to the end of the video and in paused mode, this checks for this 
        // situation and manually raises the MediaEnded event
        private void CheckEndOfVideo()
        {
            if (!IsLive && Position == EndPosition && CurrentState == SmoothStreamingMediaElementState.Paused)
            {
                if (MediaEnded != null)
                {
                    MediaEnded(this, new RoutedEventArgs());
                }
            }
        }


        private void OnSeekCompleted(TimeSpan actualSeekPosition)
        {
            // clear flag
            seekCommand.IsSeeking = false;

            // see if should play
            if (seekCommand.Play)
            {
                Play();
                seekCommand.Play = false;
            }

            // see if should seek to a new position
            if (seekCommand.Position.HasValue)
            {
                Position = seekCommand.Position.Value;
                seekCommand.Position = null;
            }

            // see if playback value changed
            if (PlaybackRate != seekCommand.LastPlaybackRate)
            {
                // store last value
                seekCommand.LastPlaybackRate = PlaybackRate;
                OnPlaybackRateChanged();
            }

            // see if there is a pending playback rate
            if (seekCommand.PlaybackRate.HasValue)
            {
                BeginSetPlaybackRate(seekCommand.PlaybackRate.Value);
                seekCommand.PlaybackRate = null;
            }

            // see if there is a pending seek to live
            if (seekCommand.StartSeekToLive)
            {
                StartSeekToLive();
                seekCommand.StartSeekToLive = false;
            }
        }


		private void OnPlaybackRateChanged()
		{
			// from Msft - CurrentPlaybackState is also not implemented yet, so manually set it
			SetCurrentPlaybackStateFromCurrentStateAndPlaybackRate();

			// from Msft - TODO: remove this when SSME fire proper trick play event
			if (TrickPlayStateChanged != null)
				TrickPlayStateChanged(this, new SimpleEventArgs<bool>(PlaybackRate != 1));

			// PlaybackRate should be a dependency property but it is not, this 
			// is a workaround by mirroring the value to a bindable property
			PlaybackRateDisplay = PlaybackRate;

			// the SSME control does not have a PlaybackRateChanged event, this is 
			// another workaround that raises the event when the property changes
			ControlHelper.RaiseEvent(PlaybackRateChanged, this);
		}

		private void Application_FullScreenChanged(object sender, EventArgs e)
		{
			// use a different max bitrate when in fullscreen or not
			UpdateMaximumPlaybackBitrate();
		}

		// get the max bitrate for the selected streams
		private void UpdateSelectedStreamsMaximumBitrate()
		{
			maxSelectedStreamBitrate = 0;

			// get video stream
            var streamResult = (from stream in SelectedStreams ?? Enumerable.Empty<StreamInfo>()
								where stream.Name == "video"
								select stream).SingleOrDefault();

			if (streamResult != null)
			{
				// get the max TrackInfo.Bitrate
				maxSelectedStreamBitrate = (from track 
					in streamResult.SelectedTracks
					select track.Bitrate).Max();
			}
			
			UpdateMaximumPlaybackBitrate();
		}

		private void UpdateMaximumPlaybackBitrate()
		{
		    ulong maxBitrate = maxSelectedStreamBitrate;
            //Commenting out this block because it is causing the assigned MaxBitrate to
            //be ignored - Kevin Rohling 11/17/09 10:41 AM
            //// TODO: workaround since the SSME does not expose the max bitrate possible, 
            //// which is not the same as the max bitrate for the selected streams, so use
            //// a hard coded value for now until it's exposed from the SSME control
            //ulong maxBitrate = Application.Current.Host.Content.IsFullScreen ?
            //    maxSelectedStreamBitrate : 1500000;

			if (MaximumPlaybackBitrate != maxBitrate)
			{
				// store new value
				MaximumPlaybackBitrate = maxBitrate;

				// raise changed event
				if (MaximumPlaybackBitrateChanged != null)
				{
					MaximumPlaybackBitrateChanged(this, new RoutedEventArgs());
				}
			}
		}

		#region dependency property callbacks

		private static void OnRetryIntervalPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			CoreSmoothStreamingMediaElement source = d as CoreSmoothStreamingMediaElement;
			source.OnRetryIntervalChanged();
		}

		private void OnRetryIntervalChanged()
		{
			if (retryMonitor != null)
			{
				retryMonitor.RetryInterval = RetryInterval;
			}
		}

		private static void OnRetryDurationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			CoreSmoothStreamingMediaElement source = d as CoreSmoothStreamingMediaElement;
			source.OnRetryDurationChanged();
		}

		private void OnRetryDurationChanged()
		{
			if (retryMonitor != null)
			{
				retryMonitor.RetryDuration = RetryDuration;
			}
		}

		private static void OnSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			CoreSmoothStreamingMediaElement source = d as CoreSmoothStreamingMediaElement;
			source.OnSourceChanged();
		}

		private static void OnSmoothStreamingSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			CoreSmoothStreamingMediaElement source = d as CoreSmoothStreamingMediaElement;
			source.OnSmoothStreamingSourceChanged();
		}

		private void OnSourceChanged()
		{
			if (Source != null)
			{
				// clear flag, set in the Play method
				pendingPlay = false;

				// reset duration, updated when video is opened
				estimatedDuration = TimeSpan.Zero;

				// load a progressive download video
				base.Source = this.Source;
			}
		}

        private void OnSmoothStreamingSourceChanged()
        {
            // cancel any pending auto retries, for a previous failed video stream
            ResetAutoRetry();
            SetSmoothStreamingVideo(this.SmoothStreamingSource);
        } 

		// set the base class SmoothStreamingSource
		internal void SetSmoothStreamingVideo(Uri streamSource)
		{
			// clear flag, set in the Play method
			pendingPlay = false;

			// reset duration, updated when video is opened
			estimatedDuration = TimeSpan.Zero;

			// reset max bitrate
			maxSelectedStreamBitrate = 0;
			UpdateMaximumPlaybackBitrate();

            // reset playback rate display
            PlaybackRateDisplay = 1.0;

			// load smooth streaming video
			base.SmoothStreamingSource = streamSource;
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

		/*
		 * Code to test Retry Slates in debug view *
		public void Test_SetRetryState(RetryState state)
		{
			retryMonitor.RetryState = state;
			switch (state)
			{
				case RetryState.None: RetrySuccessful(this, null); break;
				case RetryState.Retrying: Retrying(this, null); break;
				case RetryState.RetriesFailed: MediaFailed(this, null); break;
			}
		}
		*/

        // override, don't execute if currently seeking
        public new bool StartSeekToLive()
        {
            if (seekCommand.IsSeeking)
            {
                seekCommand.StartSeekToLive = true;
                return false;
            }

            seekCommand.IsSeeking = true;
            return base.StartSeekToLive();
        }


	}
}
