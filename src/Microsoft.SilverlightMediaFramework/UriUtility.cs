using System;

namespace Microsoft.SilverlightMediaFramework
{
	public static class UriUtility
	{
		public static Uri MakeAbsoluteUri(this Uri uriValue, Uri baseUri)
		{
			if(!uriValue.IsAbsoluteUri)
				return new Uri(baseUri, uriValue.ToString());

			return uriValue;
		}

		public static string MakeAbsoluteUriString(this Uri uriValue, Uri baseUri)
		{
			return MakeAbsoluteUri(uriValue, baseUri).ToString();
		}
	}
}
