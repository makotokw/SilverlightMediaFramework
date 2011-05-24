using System.Windows.Media;

namespace Microsoft.SilverlightMediaFramework
{
    public sealed class Utility
    {
		public static Color ConvertHexToColor(string hexColor)
		{
			if (hexColor != null)
			{
				hexColor = hexColor.Replace("#", "");
				byte r = System.Convert.ToByte(hexColor.Substring(0, 2), 16);
				byte g = System.Convert.ToByte(hexColor.Substring(2, 2), 16);
				byte b = System.Convert.ToByte(hexColor.Substring(4, 2), 16);
				
				return Color.FromArgb(System.Convert.ToByte(255), r, g, b);
			}
			else 
				return Color.FromArgb(System.Convert.ToByte(255), 0, 0, 0);
		}
    }
}
