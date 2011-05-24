﻿using System;

namespace Microsoft.SilverlightMediaFramework.Logging
{
	public abstract class Log
	{
		public object Sender { get; set; }
		public string Message { get; set; }
		public DateTime TimeStamp { get; private set; }

		public Log()
		{
			TimeStamp = DateTime.Now;
		}
	}
}
