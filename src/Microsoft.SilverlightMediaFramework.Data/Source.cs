using System;

namespace Microsoft.SilverlightMediaFramework.Data
{
  public class Source : MergeableObject
  {
    #region Fields

    private string name;
    private TimeSpan interval;
    private Uri url;

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

    public TimeSpan Interval
    {
      get { return interval; }
      set
      {
        if (interval != value)
        {
          interval = value;
          OnPropertyChanged("Interval");
        }
      }
    }

    public Uri Url
    {
      get { return url; }
      set
      {
        if (url != value)
        {
          url = value;
          OnPropertyChanged("Url");
        }
      }
    }

    #endregion

    #region Methods

    public override void Merge(MergeableObject obj)
    {
      if (!(obj is Source))
        throw new ArgumentException("Object must be an instance of the Source class.", "obj");

      Source source = (Source)obj;

      Interval = source.Interval;
      Url = source.Url;
      Name = source.Name;

      OnMerged();
    }

    #endregion
  }
}
