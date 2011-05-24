using System;
using System.Xml.Linq;

namespace Microsoft.SilverlightMediaFramework.Data.Settings
{
	/// <summary>
	/// Defines the metadata parameters and source settings used by the MetadataClient class.
	/// </summary>
	public class SettingsClient
	{
		#region Events

		public event EventHandler<SimpleEventArgs<SettingsBase>> RequestCompleted;
		public event EventHandler<SimpleEventArgs<Exception>> RequestFailed;
		public event EventHandler<SimpleEventArgs<Double>> RequestProgressChanged;

		#endregion

		#region Fields

		private Request settingsRequest;
		private Source source;
		private object settingsLock = new object();
		private string property;
		private object context;

		#endregion

		#region Constructors

		public SettingsClient(Uri uri)
			: this (uri, TimeSpan.MaxValue)
		{
		}

		public SettingsClient(Uri uri, TimeSpan interval)
		{
			this.source = new Source() { Url = uri, Name = "Settings", Interval = interval };
			this.property = string.Empty;
			this.context = null;
		}
		
		public SettingsClient(Source source)
		{
			this.source = source;
			this.property = string.Empty;
			this.context = null;
		}

		public SettingsClient(object context, string property)
		{
			this.source = null;
			this.property = property;
			this.context = context;
		}

		#endregion

		#region Methods

		public void Fetch()
		{
			if (source != null)
			{
				settingsRequest = new Request(source);
			}
			else
			{
				settingsRequest = new Request(context, property);
			}
			settingsRequest.RequestCompleted += metadataClient_RequestCompleted;
			settingsRequest.RequestFailed += metadataClient_RequestFailed;
			settingsRequest.RequestProgressChanged += metadataClient_RequestProgressChanged;
			settingsRequest.Start();
		}

		void metadataClient_RequestProgressChanged(object sender, SimpleEventArgs<double> e)
		{
			if (RequestProgressChanged != null)
			{
				RequestProgressChanged(this, new SimpleEventArgs<double>(e.Result));
			}
		}

        private void metadataClient_RequestCompleted(object sender, SimpleEventArgs<string> e)
        {
			lock (settingsLock)
			{
				XDocument doc = XDocument.Parse(e.Result);
				SettingsBase settings = SettingsMapper.ParseDocument(doc);
				if (RequestCompleted != null)
				{
					RequestCompleted(this, new SimpleEventArgs<SettingsBase>(settings));
				}
			}
        }

		private void metadataClient_RequestFailed(object sender, SimpleEventArgs<Exception> e)
		{
			if (RequestFailed != null)
			{
				RequestFailed(this, new SimpleEventArgs<Exception>(e.Result));
			}
		}

		#endregion
	}
}