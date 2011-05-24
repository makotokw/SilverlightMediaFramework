using Microsoft.SilverlightMediaFramework.Logging;

namespace Microsoft.SilverlightMediaFramework.Player
{
	public class PlayerLog : Log
	{
		public PlayerLogType LogType { get; private set; }
		public object[] Args { get; private set; }

		public PlayerLog(PlayerLogType logType, params object[] args)
			: base()
		{
			LogType = logType;
			Args = args;
		}		
	}

	public enum PlayerLogType
	{
		AdCompleted,
        AquireLicenseCompleted,
		BufferingStatusChanged,
		DownloadBitrateChange,
		DownloadProgressChanged,
		FastForwardClicked,
        FullScreenChanged,
		InStreamDataError,
        JumpToLiveClicked,
		MarkerReached,
		MarkersSkipped,
		MarkerSkippedInto,
        MediaCurrentStateChanged,
		MediaDied,
		MediaEnded,
		MediaFailed,
		MediaFailedRetry,
		MediaOpened,        
		MediaRetrySucceeded,
		MuteClicked,
		NextChapterClicked,
		PlayControlClicked,
		PlayStateChanged,
		PlayVideo,
        PositionAvailableChanged,
		PreviousChapterClicked,
        ReplayClicked,
        RewindClicked,
		ScrubCompleted,
		ScrubStarted,
		SlowMotionClicked,
        SourceChanged,
		StatusTick,
		UnMuteClicked,
        VideoOutputConnectors,
		VolumeLevelChanged,
		SmoothStreamingError,
		RetryAttempt
	}
}
