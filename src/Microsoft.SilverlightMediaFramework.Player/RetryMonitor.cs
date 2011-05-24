using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.Media.SmoothStreaming;

namespace Microsoft.SilverlightMediaFramework.Player
{
	// shim SSME helper, does two things: 1) monitors the state of the SSME control
	// so it can be restored later, and 2) encapsulates the retry and auto retry logic
	internal class RetryMonitor
	{
		// events
		public event EventHandler<SimpleEventArgs<Exception>> Retrying;
		public event RoutedEventHandler RetrySuccessful;
		public event EventHandler<ExceptionRoutedEventArgs> RetryFailed;
		public event RoutedEventHandler RetryAttempted;

		// the ssme control monitoring
		private CoreSmoothStreamingMediaElement mediaElement;

		// status, get last good state so can be restored
		private DispatcherTimer statusTimer;

		// retry
		private DispatcherTimer retryTimer;
		private DateTime startTime;
		private TimeSpan retryInterval;
		internal RetryState RetryState { get; set; }
		internal TimeSpan RetryDuration { get; set; }
		internal int RetryAttempt { get; private set; }

		internal TimeSpan RetryInterval 
		{
			get 
			{
				return retryInterval;
			}
			set
			{
				retryInterval = value;
				retryTimer.Interval = retryInterval;
			}
		}

		// state information that can be restored
		internal Uri LastSmoothStreamingSource { get; set; }
		internal TimeSpan LastPosition { get; set; }

		public RetryMonitor(CoreSmoothStreamingMediaElement mediaElement)
		{
			// store media element monitoring
			this.mediaElement = mediaElement;

			// status timer
			statusTimer = new DispatcherTimer();
			statusTimer.Interval = TimeSpan.FromMilliseconds(1000);
			statusTimer.Tick += statusTimer_Tick;
	
			// retry timer
			retryTimer = new DispatcherTimer();
			retryTimer.Interval = retryInterval;
			retryTimer.Tick += retryTimer_Tick;

			// events, note use the base class SmoothStreamingMediaElement
			// events since SmoothStreamingMediaElement shadows some events
			SmoothStreamingMediaElement baseMediaElement = (SmoothStreamingMediaElement)mediaElement;
			baseMediaElement.MediaOpened += mediaElement_MediaOpened;
			baseMediaElement.MediaEnded += mediaElement_MediaEnded;
			baseMediaElement.MediaFailed += mediaElement_MediaFailed;
			//Breaking Change: Moving PDC team from SSME build 604.9 to 604.3
            //SmoothStreamingMediaElement.ManifestReady event is no longer
            //available --Kevin Rohling (11/5/2009 4:20PM)
            //baseMediaElement.ManifestReady += baseMediaElement_ManifestReady;
			
			ResetAutoRetry();
		}

		// retry loading the stream source, only retry one time
		internal void Retry()
		{
			Retry(TimeSpan.Zero);
		}

		// retry loading the stream source for the specified amount of time
		internal void Retry(TimeSpan retryDuration)
		{
			// adjust time so it retries for the time specified, the retry
			// duration is an override, and the RetryDuration property
			// should not be updated with the new value
			RetryState = RetryState.Retrying;
			startTime = DateTime.Now.Subtract(RetryDuration - retryDuration);

			if (Retrying != null)
			{
				Retrying(this, null);
			}

			ResetSource();
		}

		private void ResetSource()
		{
			RetryAttempt++;

			if (RetryAttempted != null)
			{
				RetryAttempted(this, new RoutedEventArgs());
			}

			// retry setting the source, note call SetSmoothStreamingVideo instead of 
			// setting the SmoothStreamingSource property, this is required since the
			// SmoothStreamingSource property resets any auto retry operations
			mediaElement.SetSmoothStreamingVideo(null);
			mediaElement.SetSmoothStreamingVideo(new Uri(LastSmoothStreamingSource.AbsoluteUri));
		}

		// stop and reset auto retrys
		internal void ResetAutoRetry()
		{
			RetryState = RetryState.None;
			retryTimer.Stop();
		}

		// status timer
		private void statusTimer_Tick(object sender, EventArgs e)
		{
			// make sure should still collect monitor information
			if (statusTimer.IsEnabled)
			{
				// get retry values
				LastPosition = mediaElement.Position;
			}
		}

		// retry timer
		private void retryTimer_Tick(object sender, EventArgs e)
		{
			retryTimer.Stop();
			ResetSource();
		}
		
		private void mediaElement_MediaOpened(object sender, RoutedEventArgs e)
		{
			// store values that can be restored later
			LastSmoothStreamingSource = mediaElement.SmoothStreamingSource;

			// see if this is the result of a retry
			if (RetryState == RetryState.Retrying)
			{
				OnRetrySucceeded();
			}

			// start monitor timer
			statusTimer.Start();
		}

		private void mediaElement_MediaEnded(object sender, RoutedEventArgs e)
		{
			// stop monitor timer
			statusTimer.Stop();
			
			// media was loaded but ended, stop auto retry
			ResetAutoRetry();
		}

		private void  mediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
		{
			// store source when fail, otherwise don't know which source was trying to be opened
			LastSmoothStreamingSource = mediaElement.SmoothStreamingSource;

			// stop monitor timer
			statusTimer.Stop();
			
			// auto retry

			// see if first time media failed
			if (RetryState == RetryState.None)
			{
				OnAutoRetryStart(e.ErrorException);
			}
			
			// see if should try again
			TimeSpan elapsedTime = DateTime.Now.Subtract(startTime);
			if (RetryDuration > elapsedTime)
			{
				// retry again, first start the timer and 
				// retry during the timer tick event
				retryTimer.Start();
			}
			else
			{
				// time expired, don't retry again
				OnRetryFailed(e);
			}
		}


		private void OnAutoRetryStart(Exception exception)
		{
			// store current time so can tell when time expires
			startTime = DateTime.Now;
			RetryState = RetryState.Retrying;

			if (Retrying != null)
			{
				// TODO:  jack
				Retrying(this, new SimpleEventArgs<Exception>(exception));
			}
		}

		private void OnRetrySucceeded()
		{
			RetryAttempt = 0;

			ResetAutoRetry();

			// restore state of player
			if (mediaElement.IsLive && LastPosition == TimeSpan.Zero)
			{
				// go to live if a live feed and don't have a last position,
				// most likely this is the result of the first load attempt
				mediaElement.StartSeekToLive();
			}
			else
			{	
				// restore position		
				mediaElement.Position = LastPosition;
			}

			if (RetrySuccessful != null)
			{
				RetrySuccessful(this, new RoutedEventArgs());
			}
		}

		private void OnRetryFailed(ExceptionRoutedEventArgs e)
		{
			// gave up on auto retry
			ResetAutoRetry();
			RetryState = RetryState.RetriesFailed;

			if (RetryFailed != null)
			{
				RetryFailed(this, e);
			}
		}
	}
}
