﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.SilverlightMediaFramework.Logging;

namespace Microsoft.SilverlightMediaFramework.Test
{
    public partial class MainPage : UserControl
    {
        public MainPage()
        {
            InitializeComponent();
            Logger.LogReceived += new EventHandler<SimpleEventArgs<Log>>(Logger_LogReceived);
        }

        void Logger_LogReceived(object sender, SimpleEventArgs<Log> e)
        {
            Debug.WriteLine(e.Result.Message);
        }
    }
}
