using System;

namespace Microsoft.SilverlightMediaFramework.Data.Settings
{
  public class Parameter : MergeableObject
  {
    #region Fields

    private string name;
    private string value;

    #endregion

    #region Properties

    public override string MergeID
    {
      get { return Name; }
    }

    public string Name
    {
      get { return name; }
      set
      {
        if (name != value)
        {
          name = value;
          OnPropertyChanged("Name");
        }
      }
    }

    public string Value
    {
      get { return value; }
      set
      {
        if (this.value != value)
        {
          this.value = value;
          OnPropertyChanged("Value");
        }
      }
    }

    #endregion

    #region Methods

    public override void Merge(MergeableObject obj)
    {
      if (!(obj is Parameter))
        throw new ArgumentException("Object must be an instance of the SettingsParameter class.", "obj");

      Parameter parameter = (Parameter)obj;

      Name = parameter.Name;
      Value = parameter.Value;

      OnMerged();
    }

    public override bool Equals(MergeableObject obj)
    {
      if (!(obj is Parameter))
        throw new ArgumentException("Object must be an instance of the SettingsParameter class.", "obj");

      Parameter parameter = (Parameter)obj;

      return (parameter != null) ? (parameter.Name == Name && parameter.Value == Value) : false;
    }

    public override int GetHashCode()
    {
      return (name != null) ? name.GetHashCode() : 0;
    }

    #endregion
  }
}