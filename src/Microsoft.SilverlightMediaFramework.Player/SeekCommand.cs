using System;

namespace Microsoft.SilverlightMediaFramework.Player
{
	internal class SeekCommand
	{
		// is currently seeking or not
		public bool IsSeeking;
		
		// if should play when seek complets
		public bool Play;
		
		// if should seek to a new position when seek completes
		public TimeSpan? Position;
		
		// if should set a new playback rate
		public double? PlaybackRate;
		
		// store the last playback rate that was set
		public double LastPlaybackRate;

        // if should seek to live when seek completes
        public bool StartSeekToLive;

	}
}
