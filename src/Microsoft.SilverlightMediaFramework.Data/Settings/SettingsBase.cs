using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Microsoft.SilverlightMediaFramework.Data.Settings
{
  public class SettingsBase : MergeableObject
  {
    #region Properties

    public MergeableCollection<Parameter> Parameters { get; set; }
    public MergeableCollection<Source> DataSources { get; set; }

    public override string MergeID
    {
      get { return "1"; }
    }

    #endregion

    #region Constructors

    public SettingsBase()
    {
      Parameters = new MergeableCollection<Parameter>();
      DataSources = new MergeableCollection<Source>();

      DataSources.CollectionChanged += CollectionChanged;
      Parameters.CollectionChanged += CollectionChanged;
    }

    #endregion

    #region Methods

    private void CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
      if (e.Action == NotifyCollectionChangedAction.Replace || e.Action == NotifyCollectionChangedAction.Add)
      {
        if (e.NewItems[0] is Source)
        {
          string propertyName = (e.NewItems[0] as Source).Name;
          if (HasProperty(this, propertyName))
            OnPropertyChanged(propertyName);
        }
        else if (e.NewItems[0] is Parameter)
        {
          string propertyName = (e.NewItems[0] as Parameter).Name;
          if (HasProperty(this, propertyName))
            OnPropertyChanged(propertyName);
        }
      }
    }

    protected string GetParameterValue(string name)
    {
      Parameter parameter = this.Parameters.FirstOrDefault(p => p.Name == name);
      return (parameter != null) ? parameter.Value : string.Empty;
    }

	protected double? GetParameterDouble(string name)
	{
		double result;
		if(double.TryParse(GetParameterValue(name), NumberStyles.Any, CultureInfo.InvariantCulture, out result))
		{
			return result;
		}

		return null;
	}

	protected int? GetParameterInt32(string name)
	{
		int result;
		if(int.TryParse(GetParameterValue(name), NumberStyles.Any, CultureInfo.InvariantCulture, out result))
		{
			return result;
		}

		return null;
	}

	protected long? GetParameterInt64(string name)
	{
		long result;
		if(long.TryParse(GetParameterValue(name), NumberStyles.Any, CultureInfo.InvariantCulture, out result))
		{
			return result;
		}

		return null;
	}

	protected bool? GetParameterBoolean(string name)
	{
		bool result;
		if(bool.TryParse(GetParameterValue(name), out result))
		{
			return result;
		}

		return null;
	}

    public Source GetDataSource(string name)
    {
      return this.DataSources.FirstOrDefault(p => p.Name == name);
    }

    public override void Merge(MergeableObject obj)
    {
      if (!(obj is SettingsBase))
        throw new ArgumentException("Object must be an instance of the SettingsBase class.", "obj");

      SettingsBase settings = (SettingsBase)obj;

      Parameters.Merge(settings.Parameters);
      DataSources.Merge(settings.DataSources);

      OnMerged();
    }

    public override bool Equals(MergeableObject obj)
    {
      if (!(obj is SettingsBase))
        throw new ArgumentException("Object must be an instance of the SettingsBase class.", "obj");

      SettingsBase settings = (SettingsBase)obj;

      return Parameters.Equals(settings.Parameters) && DataSources.Equals(settings.DataSources);
    }

    protected bool HasProperty(object context, string propertyName)
    {
      Type type = context.GetType();
      PropertyInfo property = type.GetProperty(propertyName);

      return property == null ? false : true;
    }

    #endregion
  }
}
