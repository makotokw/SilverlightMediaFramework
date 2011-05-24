using System;
using System.Windows.Browser;

namespace Microsoft.SilverlightMediaFramework
{
    [ScriptableType]
    public class SimpleEventArgs<T> : EventArgs
    {
        [ScriptableMember]
        public T Result { get; set; }

        public SimpleEventArgs(T result)
        {
            Result = result;
        }
    }
}
