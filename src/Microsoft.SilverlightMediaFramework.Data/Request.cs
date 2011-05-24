using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Windows.Threading;
using Microsoft.SilverlightMediaFramework.Logging;

namespace Microsoft.SilverlightMediaFramework.Data
{
	/// <summary>
	/// Sends requests for data (i.e. XML files) to allow objects and collections
	/// to be periodically synchronized with the latest data defined on the server.
	/// </summary>
	public class Request
	{
		#region Events

		public event EventHandler<SimpleEventArgs<string>> RequestCompleted;
		public event EventHandler<SimpleEventArgs<Exception>> RequestFailed;
		public event EventHandler<SimpleEventArgs<Double>> RequestProgressChanged;

		#endregion

		#region Fields

		private Source source = null;
		private DispatcherTimer timer = null;
		private string boundPropertyName = string.Empty;
		private object context = null;

		#endregion

		#region Properties

		public Source Source
		{
			get
			{
				return source;
			}
			set
			{
				if (source != value)
				{
					source = value;
				}
			}
		}

		#endregion

		#region Constructors

		/// <summary>
		/// Setup a non-reoccurring data request.
		/// The Start method must be called to begin the request.
		/// </summary>
		/// <param name="source"></param>
		public Request(Uri source)
			: this(new Source() { Url = source, Name = Guid.NewGuid().ToString(), Interval = TimeSpan.MaxValue })
		{
		}

		/// <summary>
		/// Setup a reoccurring data client.
		/// The Start method must be called to begin the request.
		/// </summary>
		/// <param name="source"></param>
		public Request(Source source)
		{
			if (source == null)
				throw new ArgumentNullException("Source");

			this.source = source;
			if (source.Interval != TimeSpan.MaxValue)
			{
				this.timer = new DispatcherTimer();
				timer.Tick += timer_Tick;
				timer.Interval = source.Interval;
			}
		}

		/// <summary>
		/// Setup a reoccurring data client which is bound to a source property of an object.
		/// The Start method must be called to begin the request.
		/// </summary>
		/// <param name="source"></param>
		public Request(object context, string propertyName)
		{
			if (context == null)
			{
				throw new ArgumentNullException("Context must not be null.");
			}

			if (!(context is INotifyPropertyChanged))
			{
				throw new ArgumentException("Context does not implement INotifyPropertyChanged.");
			}

			this.boundPropertyName = propertyName;
			this.context = context;
			this.source = GetSourceFromContext(context, propertyName);

			(context as INotifyPropertyChanged).PropertyChanged += context_PropertyChanged;

			if (source != null && source.Interval != TimeSpan.MaxValue)
			{
				this.timer = new DispatcherTimer();
				timer.Tick += timer_Tick;
				timer.Interval = source.Interval;
			}
		}

		private void context_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (this.boundPropertyName != string.Empty && e.PropertyName == this.boundPropertyName)
			{
				timer.Stop();
				
				this.source = GetSourceFromContext(this.context, this.boundPropertyName);
				if (this.source != null)
				{
					timer.Interval = source.Interval;
				}
				
				timer.Start();
			}
		}

		private Source GetSourceFromContext(object context, string propertyName)
		{
			Source source = null;

			Type type = context.GetType();
			PropertyInfo property = type.GetProperty(this.boundPropertyName);

			source = property.GetValue(context, null) as Source;

			return source;
		}

		#endregion

		#region Methods

		/// <summary>
		/// Starts processing metadata requests.
		/// </summary>
		public void Start()
		{
			BeginRequest();
			
			if (this.source != null && this.timer != null)
			{
				lock (timer)
				{
					if (timer.IsEnabled == false)
					{
						timer.Start();

						Logger.Log(new DebugLog()
						{
							Message = string.Format("Client started. Name: {0} Interval: {2} Url: {1}",
											source.Name,
											source.Url,
											source.Interval),
							Sender = this
						});
					}
				}
			}
		}

		/// <summary>
		/// Stops processing metadata requests.
		/// </summary>
		public void Stop()
		{
			if (this.timer != null && this.source != null)
			{
				lock (timer)
				{
					if (timer.IsEnabled)
					{
						timer.Stop();

						Logger.Log(new DebugLog()
						{
							Message = string.Format("Client stopped. Name: {0} Interval: {2} Url: {1}",
											source.Name,
											source.Url,
											source.Interval),
							Sender = this
						});
					}
				}
			}
		}

		public void BeginRequest()
		{
			Uri cacheBustedUri = CacheBust(this.source.Url);
			WebClient webClient = new WebClient();
			webClient.DownloadStringCompleted += webClient_DownloadStringCompleted;
			webClient.DownloadProgressChanged += webClient_DownloadProgressChanged;
			webClient.DownloadStringAsync(cacheBustedUri);

			Logger.Log(new DebugLog() { Sender = this, Message = String.Format("Request sent. Url: {0} ", cacheBustedUri) });
		}

		private void webClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			if (RequestProgressChanged != null)
			{
				RequestProgressChanged(this, new SimpleEventArgs<double>(e.ProgressPercentage));
			}
		}

		private void webClient_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
		{
			SimpleEventArgs<Exception> requestFailedArgs = null;

			if (e.Error == null && !string.IsNullOrEmpty(e.Result))
			{
				try
				{
					if (RequestCompleted != null)
					{
						RequestCompleted(this, new SimpleEventArgs<string>(e.Result));
					}

					Logger.Log(new DebugLog() { Sender = this, Message = String.Format("Request completed. Url: {0} ", this.Source.Url) });
				}
				catch (Exception x)
				{
					requestFailedArgs = new SimpleEventArgs<Exception>(x);
				}
			}
			else
			{
				requestFailedArgs = new SimpleEventArgs<Exception>(e.Error);
			}

			if (requestFailedArgs != null)
			{
				if (RequestFailed != null)
				{
					RequestFailed(this, requestFailedArgs);
				}

				Logger.Log(new DebugLog() { Sender = this, Message = String.Format("Request failed. Url: {0} ", this.Source.Url) });
			}

			(sender as WebClient).DownloadStringCompleted -= webClient_DownloadStringCompleted;
			(sender as WebClient).DownloadProgressChanged -= webClient_DownloadProgressChanged;
		}

		private void timer_Tick(object sender, EventArgs e)
		{
			DispatcherTimer timer = (DispatcherTimer)sender;
			Debug.Assert(timer != null, "Unexpected timer tick.");

			BeginRequest();
		}

		public static Uri CacheBust(Uri uri)
		{
			Uri cacheBustedUri;

			if (string.IsNullOrEmpty(uri.Query))
				cacheBustedUri = new Uri(string.Format("{0}?ignore={1}", uri, Guid.NewGuid()));
			else
				cacheBustedUri = new Uri(string.Format("{0}&ignore={1}", uri, Guid.NewGuid()));

			return cacheBustedUri;
		}

		#endregion
	}
}