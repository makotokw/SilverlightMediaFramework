
namespace Microsoft.SilverlightMediaFramework
{
	public static class StringUtility
	{
		public static string ToMaximumLength(this string value, int length)
		{
			return (value.Length > length) ? value.Substring(0, length) : value;
		}
	}
}
