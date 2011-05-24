using System;
using System.Xml.Linq;

namespace Microsoft.SilverlightMediaFramework.Data.Settings
{
	public class SettingsMapper
	{
		public static SettingsBase ParseDocument(XDocument document)
		{
			SettingsBase settings = new SettingsBase();

			foreach (XElement element in document.Descendants("Parameter"))
			{
				Parameter parameter = new Parameter()
				{
					Name = element.Attribute("Name").GetValue(),
					Value = element.Attribute("Value").GetValue()
				};

				settings.Parameters.Add(parameter);
			}

			foreach (XElement element in document.Descendants("DataSource"))
			{
				Source source = new Source();
				source.Name = element.Attribute("Name").GetValue();
				source.Url = element.Attribute("Url").GetValueAsUri();

				TimeSpan? interval = element.Attribute("Interval").GetValueAsTimeSpan();
				source.Interval = interval == null ? TimeSpan.MaxValue : interval.Value;

				settings.DataSources.Add(source);
			}

			return settings;
		}
	}
}
