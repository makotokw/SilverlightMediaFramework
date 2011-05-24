using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Xml.Linq;

namespace Microsoft.SilverlightMediaFramework.Data
{
	public abstract class InStreamDataClient<T> : IDisposable
	{
		public event EventHandler<SimpleEventArgs<T>> ItemAdded;
		public event EventHandler<SimpleEventArgs<T>> ItemRemoved;

		protected ObservableCollection<XElement> InStreamData { get; set; }

		public InStreamDataClient(ObservableCollection<XElement> inStreamData)
		{
			this.InStreamData = inStreamData;
		}

		public void Fetch()
		{
			InStreamData.CollectionChanged += OnCollectionChanged;
		}

		public void StopFetch()
		{
			InStreamData.CollectionChanged -= OnCollectionChanged;
		}

		protected virtual void OnItemAdded(T item)
		{
			var handler = ItemAdded;
			if (handler != null)
			{
				handler(this, new SimpleEventArgs<T>(item));
			}
		}

		protected virtual void OnItemRemoved(T item)
		{
			var handler = ItemRemoved;
			if (handler != null)
			{
				handler(this, new SimpleEventArgs<T>(item));
			}
		}

		protected abstract void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e);

		#region IDisposable Members

		public void Dispose()
		{
			if (InStreamData != null)
			{
				InStreamData.CollectionChanged -= OnCollectionChanged;
			}
		}

		#endregion
	}
}
