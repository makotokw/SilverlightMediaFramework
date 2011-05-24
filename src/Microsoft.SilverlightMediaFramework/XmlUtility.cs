using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.SilverlightMediaFramework
{
	/// <summary>
	/// Provides extension methods to parse XML metadata.
	/// </summary>
	public static class XmlUtility
	{
		#region Constants

		private const string ISO8601_MARKER = "T";
		private const string ET_MARKER = "ET";
		private const string NO_TOKEN = "NO";
		private const string YES_TOKEN = "YES";

		#endregion

		#region Static Variables

		private static readonly TimeSpan EdtOffset = TimeSpan.FromHours(4.0);
		private static readonly TimeSpan EstOffset = TimeSpan.FromHours(5.0);

		#endregion

		#region Static Methods

		#region Extension Method: GetValue

		public static string GetValue(this XAttribute attribute)
		{
			return (attribute != null) ? attribute.Value : null;
		}

		public static string GetValue(this XElement element)
		{
			return (element != null) ? element.Value : null;
		}

		#endregion

		#region Extension Method: GetValueAsBoolean

		public static bool? GetValueAsBoolean(this XAttribute attribute)
		{
			bool b;

			return ((attribute != null) &&
				bool.TryParse(attribute.Value, out b)) ?
				b : (bool?)null;
		}

		public static bool? GetValueAsBoolean(this XElement element)
		{
			bool b;

			return ((element != null) &&
				bool.TryParse(element.Value, out b)) ?
				b : (bool?)null;
		}

		#endregion

		#region Extension Method: GetValueAsDateTime

        public static DateTime? GetValueAsDateTime(this XElement element, IFormatProvider formatProvider)
        {
            Debug.Assert(formatProvider != null, "formatProvider is null.");

            if (String.IsNullOrEmpty(element.Value) == false)
            {
                DateTime dateTime;
                if (DateTime.TryParse(element.Value, formatProvider, DateTimeStyles.None, out dateTime))
                {
                    return dateTime;
                }
            }

            return null;
        }

		public static DateTime? GetValueAsDateTime(this XAttribute attribute, IFormatProvider formatProvider)
		{
			Debug.Assert(formatProvider != null, "formatProvider is null.");
			
			if (String.IsNullOrEmpty(attribute.Value) == false)
			{
				DateTime dateTime;
				if (DateTime.TryParse(attribute.Value, formatProvider, DateTimeStyles.None, out dateTime))
				{
					return dateTime;
				}
			}

			return null;
		}

		#endregion

		#region Extension Method: GetValueAsInt32

		public static int? GetValueAsInt32(this XAttribute attribute)
		{
			int i;

			return (attribute != null) &&
				int.TryParse(attribute.Value, out i) ?
				i : (int?)null;
		}

		public static int? GetValueAsInt32(
			this XAttribute attribute,
			IFormatProvider formatProvider)
		{
			Debug.Assert(formatProvider != null, "formatProvider is null.");

			int i;

			return (attribute != null) &&
				int.TryParse(attribute.Value,
					NumberStyles.Integer,
					formatProvider,
					out i) ?
				i : (int?)null;
		}

		public static int? GetValueAsInt32(this XElement element)
		{
			int i;

			return (element != null) &&
				int.TryParse(element.Value, out i) ?
				i : (int?)null;
		}

		public static int? GetValueAsInt32(
			this XElement element,
			IFormatProvider formatProvider)
		{
			Debug.Assert(formatProvider != null, "formatProvider is null.");

			int i;

			return (element != null) &&
				int.TryParse(
					element.Value,
					NumberStyles.Integer,
					formatProvider,
					out i) ?
				i : (int?)null;
		}

		#endregion

		#region Extension Method: GetValueAsInt64

		public static long? GetValueAsInt64(this XAttribute attribute)
		{
			long result;

			return (attribute != null) &&
				long.TryParse(attribute.Value, out result) ?
				result : (long?)null;
		}

		public static long? GetValueAsInt64(
			this XAttribute attribute,
			IFormatProvider formatProvider)
		{
			Debug.Assert(formatProvider != null, "formatProvider is null.");

			long result;

			return (attribute != null) &&
				long.TryParse(attribute.Value,
					NumberStyles.Integer,
					formatProvider,
					out result) ?
				result : (long?)null;
		}

		public static long? GetValueAsInt64(this XElement element)
		{
			long result;

			return (element != null) &&
				long.TryParse(element.Value, out result) ?
				result : (long?)null;
		}

		public static long? GetValueAsInt64(
			this XElement element,
			IFormatProvider formatProvider)
		{
			Debug.Assert(formatProvider != null, "formatProvider is null.");

			long result;

			return (element != null) &&
				long.TryParse(
					element.Value,
					NumberStyles.Integer,
					formatProvider,
					out result) ?
				result : (long?)null;
		}

		#endregion

		#region Extension Method: GetValueAsTimeSpan

		public static TimeSpan? GetValueAsTimeSpan(this XAttribute attribute)
		{
			TimeSpan result;
			return (TimeSpan.TryParse(attribute.GetValue(), out result)) ? result : (TimeSpan?)null;
		}

		public static TimeSpan? GetValueAsTimeSpan(this XElement element)
		{
			TimeSpan t;

			return (element != null) &&
				TimeSpan.TryParse(element.Value, out t) ?
				t : (TimeSpan?)null;
		}
		#endregion

		#region Extension Method: GetValueAsUri

        public static Uri GetValueAsUri(this XElement element)
        {
            Uri result;
            return (Uri.TryCreate(element.GetValue(), UriKind.RelativeOrAbsolute, out result)) ? result : null;
        }

		public static Uri GetValueAsUri(this XAttribute attribute)
		{
			Uri result;
			return (Uri.TryCreate(attribute.GetValue(), UriKind.RelativeOrAbsolute, out result)) ? result : null;
		}

		public static Uri GetValueAsUri(
			this XAttribute attribute,
			UriKind uriKind)
		{
			Uri u;
			return (attribute != null) &&
				Uri.TryCreate(attribute.Value, uriKind, out u) ?
				u : null;
		}

		#endregion

		#region Extension Method: GetValueAsYesNoBoolean

		public static bool? GetValueAsYesNoBoolean(this XAttribute attribute)
		{
			string s = attribute.GetValue();

			if (!string.IsNullOrEmpty(s))
			{
				s = s.ToUpper(CultureInfo.InvariantCulture);

				if (s == YES_TOKEN)
				{
					return true;
				}
				else if (s == NO_TOKEN)
				{
					return false;
				}
			}

			return null;
		}

		#endregion

		#region Extension Method: InnerXml

		public static string InnerXml(this XElement element)
		{
			if (element == null)
			{
				return null;
			}

			StringReader stringReader = null;
			XmlReader xmlReader = null;
			try
			{
				stringReader = new StringReader(element.ToString());
				xmlReader = XmlReader.Create(stringReader);

				return xmlReader.Read() ?
					xmlReader.ReadInnerXml() :
					string.Empty;
			}
			finally
			{
				if (xmlReader != null)
				{
					((IDisposable)xmlReader).Dispose();
				}

				if (stringReader != null)
				{
					((IDisposable)stringReader).Dispose();
				}
			}
		}

		#endregion

		#region Static Method: ParseEtDateTime

		private static DateTime? ParseEtDateTime(
			string s,
			IFormatProvider formatProvider)
		{
			Debug.Assert(!string.IsNullOrEmpty(s), "s is null or empty.");
			Debug.Assert(
				s.ToUpper(CultureInfo.InvariantCulture).EndsWith(ET_MARKER),
				"s does not end with ET.");
			Debug.Assert(formatProvider != null, "formatProvider is null.");

			s = s.Substring(0, s.Length - ET_MARKER.Length);

			DateTime d;
			if (DateTime.TryParse(
					s,
					formatProvider,
					DateTimeStyles.AllowTrailingWhite | DateTimeStyles.AssumeLocal,
					out d))
			{
				return d;
			}
			else
			{
				return null;
			}
		}

		#endregion

		#region Static Method: ParseReverseIso8601DateTime

		private static DateTime? ParseReverseIso8601DateTime(string s)
		{
			Debug.Assert(!string.IsNullOrEmpty(s), "s is null or empty.");
			Debug.Assert(
				s.IndexOf(ISO8601_MARKER) != -1,
				"No ISO8601 marker in s.");
			Debug.Assert(
				!s.EndsWith(ISO8601_MARKER),
				"s ends with ISO8601 marker.");

			int i = s.IndexOf(ISO8601_MARKER);
			string dateString = s.Substring(0, i);
			string timeString = s.Substring(i + 1);

			DateTime d;
			TimeSpan t;

			if (
				DateTime.TryParse(
					dateString,
					CultureInfo.
					InvariantCulture,
					DateTimeStyles.None,
					out d) &&
				TimeSpan.TryParse(timeString, out t))
			{
				return d + t;
			}
			else
			{
				return null;
			}
		}

		#endregion

		#endregion
	}
}