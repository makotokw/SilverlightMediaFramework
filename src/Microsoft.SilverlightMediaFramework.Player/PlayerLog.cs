using Microsoft.SilverlightMediaFramework.Logging;

namespace Microsoft.SilverlightMediaFramework.Player
{
	public class PlayerLog : Log
	{
		public PlayerLogType LogType { get; private set; }
		public object[] Args { get; private set; }

		internal PlayerLog(PlayerLogType logType, params object[] args)
			: base()
		{
			LogType = logType;
			Args = args;
		}		
	}

	public enum PlayerLogType
	{
		AdCompleted,
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
		PreviousChapterClicked,
        ReplayClicked,
        RewindClicked,
		ScrubCompleted,
		ScrubStarted,
		SlowMotionClicked,
		StatusTick,
		UnMuteClicked,
		VolumeLevelChanged,
		SmoothStreamingError,
		RetryAttempt
	}
}
