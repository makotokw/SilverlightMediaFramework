using System;

namespace Microsoft.SilverlightMediaFramework.Data
{
	public abstract class DataClient<T> : IDisposable
	{
		public event EventHandler<SimpleEventArgs<T>> FetchCompleted;
		public event EventHandler<SimpleEventArgs<Exception>> FetchFailed;
		public event EventHandler<SimpleEventArgs<double>> FetchProgressChanged;

		protected Uri Uri { get; set; }
		protected Source Source { get; set; }
		protected object Context { get; set; }
		protected string PropertyName { get; set; }
		protected Request Request { get; set; }

		public DataClient(Uri uri)
		{
			this.Uri = uri;
		}

		public DataClient(Source source)
		{
			this.Source = source;
		}

		public DataClient(object context, string propertyName)
		{
			this.Context = context;
			this.PropertyName = propertyName;
		}

		public virtual void Fetch()
		{
			if (Request != null)
			{
				StopFetch();
			}

			if (Uri != null)
			{
				Request = new Request(Uri);
			}
			else if (Source != null)
			{
				Request = new Request(Source);
			}
			else
			{
				Request = new Request(Context, PropertyName);
			}

			Request.RequestCompleted += OnRequestCompleted;
			Request.RequestFailed += OnRequestFailed;
			Request.RequestProgressChanged += OnRequestProgressChanged;
			Request.Start();
		}

		public virtual void StopFetch()
		{
			Request.RequestCompleted -= OnRequestCompleted;
			Request.RequestFailed -= OnRequestFailed;
			Request.RequestProgressChanged -= OnRequestProgressChanged;
			Request.Stop();
		}

		protected virtual void OnFetchCompleted(T item)
		{
			var handler = FetchCompleted;
			if (handler != null)
			{
				handler(this, new SimpleEventArgs<T>(item));
			}
		}

		protected virtual void OnFetchFailed(Exception e)
		{
			var handler = FetchFailed;
			if (handler != null)
			{
				handler(this, new SimpleEventArgs<Exception>(e));
			}
		}

		protected virtual void OnFetchProgressChanged(double progress)
		{
			var handler = FetchProgressChanged;
			if (handler != null)
			{
				handler(this, new SimpleEventArgs<double>(progress));
			}
		}

		protected virtual void OnRequestFailed(object sender, SimpleEventArgs<Exception> e)
		{
			OnFetchFailed(e.Result);
		}

		protected virtual void OnRequestProgressChanged(object sender, SimpleEventArgs<double> e)
		{
			OnFetchProgressChanged(e.Result);
		}

		protected abstract void OnRequestCompleted(object sender, SimpleEventArgs<string> e);

		#region IDisposable Members

		public void Dispose()
		{
			Request.RequestCompleted -= OnRequestCompleted;
			Request.RequestFailed -= OnRequestFailed;
			Request.RequestProgressChanged -= OnRequestProgressChanged;
		}

		#endregion
	}
}
