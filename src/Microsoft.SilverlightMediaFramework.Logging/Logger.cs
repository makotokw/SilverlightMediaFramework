using System;
using System.Windows;

namespace Microsoft.SilverlightMediaFramework.Logging
{
	public static class Logger
	{
		#region Events

		public static event EventHandler<SimpleEventArgs<Log>> LogReceived;

		#endregion

		#region Methods

		public static void Log(Log log)
		{
			if (log is DebugLog)
			{
				#if !DEBUG
					return;
				#endif
			}

			OnLogReceived(log.Sender, log);
		}

        public static void OnLogReceived(object sender, Log log)
        {
            if (LogReceived != null)
            {
                // can receive events from worker threads, 
                // make sure on ui thread before raise event
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    LogReceived(sender, new SimpleEventArgs<Log>(log));
                });
            }
        }


		#endregion
	}
}
