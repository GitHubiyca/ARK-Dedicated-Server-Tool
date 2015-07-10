﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ARK_Server_Manager
{
    /// <summary>
    /// Interaction logic for CommandLine.xaml
    /// </summary>
    public partial class CommandLine : Window
    {
        public CommandLine(string commandLine)
        {
            InitializeComponent();
            this.DataContext = commandLine;
        }

        private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Clipboard.SetText(this.DataContext as string);
            MessageBox.Show("Done!", "Copied to clipboard", MessageBoxButton.OK);
        }
    }
}
