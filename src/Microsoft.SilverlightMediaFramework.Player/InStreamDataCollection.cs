using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml.Linq;
using Microsoft.SilverlightMediaFramework.Logging;
using Microsoft.Web.Media.SmoothStreaming;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.SilverlightMediaFramework.Player
{
	public class InStreamDataCollection : ObservableCollection<XElement>, IDisposable
	{
		private CoreSmoothStreamingMediaElement mediaElement;
        private List<string> dataStreams;
		private Dictionary<Guid, XElement> envelopeData;

        // flag, if should process all data streams
        public bool UseAllDataStreams { get; set; }

        // list of data streams to process
        public List<string> DataStreams
        {
            get
            {
                return dataStreams;
            }
        }

        public CoreSmoothStreamingMediaElement MediaElement
        {
            get
            {
                return mediaElement;
            }
            set
            {
                if (value != null)
                {
                    // uninitialize current event handlers
                    if (mediaElement != null)
                    {
                        mediaElement.MediaOpened -= MediaElement_MediaOpened;
                        mediaElement.TimelineEventReached -= MediaElement_TimelineEventReached;
                    }

                    mediaElement = value;
                    mediaElement.MediaOpened += MediaElement_MediaOpened;
                    mediaElement.TimelineEventReached += MediaElement_TimelineEventReached;
                }
            }
        }


		public InStreamDataCollection()
		{
            dataStreams = new List<string>();
            envelopeData = new Dictionary<Guid, XElement>();
		}

		private void MediaElement_MediaOpened(object sender, RoutedEventArgs e)
		{
			foreach (StreamInfo data in mediaElement.AvailableStreams)
			{
				Logger.Log(new DebugLog() { Sender = sender, Message = String.Format(CultureInfo.InvariantCulture, "Preload in-stream track found Name:={0}, Data:={1}", data.Name, data.Type) });

				if (data.IsSparseStream)
				{
					TrackInfo track = data.AvailableTracks.FirstOrDefault();
                    if (track != null && track.TrackData != null && ShouldProcessDataStream(track))
					{
						Logger.Log(new DebugLog() { Sender = sender, Message = String.Format(CultureInfo.InvariantCulture, "Preload in-stream data reveived with {0} strings", track.TrackData.Count) });

						foreach (TimelineEvent dataEvent in track.TrackData)
						{
							string dataString = Encoding.UTF8.GetString(dataEvent.EventData, 0, dataEvent.EventData.Length);
							Logger.Log(new DebugLog() { Sender = sender, Message = String.Format(CultureInfo.InvariantCulture, "Preload in-stream data: {0}", dataString) });
							AddElement(dataString);
						}
					}
				}
			}
		}

		private void MediaElement_TimelineEventReached(object sender, TimelineEventArgs e)
		{
			lock(this)
			{
                if (ShouldProcessDataStream(e.Track))
                {
                    Logger.Log(new DebugLog() { Sender = sender, Message = "Timeline in-stream data reveived with 1 string" });

                    string dataString = Encoding.UTF8.GetString(e.Event.EventData, 0, e.Event.EventData.Length);
                    Logger.Log(new DebugLog() { Sender = sender, Message = String.Format(CultureInfo.InvariantCulture, "Timeline in-stream data: {0}", dataString) });
                    AddElement(dataString);
                }
			}
		}

        private bool ShouldProcessDataStream(TrackInfo track)
        {
            // check if should process all streams
            if (UseAllDataStreams)
                return true;

            // see if the stream name for the track is in the data stream list
            var result = (from d in dataStreams
                          where d == track.ParentStream.Name
                          select d).FirstOrDefault();

            return (result != null);
        }

		private void AddElement(string stringData)
		{
			XElement element = null;

			try
			{
				element = XElement.Parse(stringData);
			}
			catch
			{
				Logger.Log(new PlayerLog(PlayerLogType.InStreamDataError) { Sender = this, Message = "Error parsing InStream XML." });
			}

			try
			{
                if (element != null && element.Name == "InStreamEnvelope")
                {
                    if (element.Attribute("Id") != null)
                    {
                        Guid id = new Guid(element.Attribute("Id").Value);

                        if (!envelopeData.Keys.Contains(id))
                        {
                            envelopeData.Add(id, element);
                            string action = element.Attribute("Action").Value.ToLower(CultureInfo.InvariantCulture);

                            switch (action)
                            {
                                case "add":
									AddElement(element);
                                    break;
                                case "remove":
									RemoveTarget(element);
                                    break;
                                case "replace":
									RemoveTarget(element);

									AddElement(element);
                                    break;
                            }
                        }
                    }
                    else
                    {
                        Logger.Log(new PlayerLog(PlayerLogType.InStreamDataError) { Sender = this, Message = "Error, InStreamEnvelope is not available." });
                    }
                }
			}
			catch
			{
				Logger.Log(new PlayerLog(PlayerLogType.InStreamDataError) { Sender = this, Message = "Error parsing InStreamEnvelope." });
			}
		}

		private void AddElement(XElement element)
		{
			foreach (XElement child in element.Elements())
			{
				if (!base.Contains(child))
				{
					base.Add(child);
				}
			}
		}

		private bool RemoveTarget(XElement element)
		{
			int elementCount = this.Count;
			Guid removeKey = new Guid(element.Attribute("TargetID").Value);
			XElement removeElement = envelopeData.ContainsKey(removeKey) ? envelopeData[removeKey] : null;
			if (removeElement != null)
			{
				foreach (XElement child in removeElement.Elements())
				{
					base.Remove(child);
				}
			}

			if (elementCount == this.Count)
			{
				foreach (XElement envelope in envelopeData.Values)
				{
					if (envelope.Attribute("TargetID").Value != null
						&& envelope.Attribute("TargetID").Value != string.Empty
						&& removeKey == new Guid(envelope.Attribute("TargetID").Value))
					{
						foreach (XElement child in envelope.Elements())
						{
							base.Remove(child);
						}
					}
				}
			}

			if (elementCount != this.Count)
				return true;

			return false;
		}

		#region IDisposable Members

		public void Dispose()
		{
            mediaElement.MediaOpened -= MediaElement_MediaOpened;
            mediaElement.TimelineEventReached -= MediaElement_TimelineEventReached;
		}

		#endregion
	}
}
