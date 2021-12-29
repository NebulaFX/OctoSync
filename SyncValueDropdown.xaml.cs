using System;
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

namespace OctoSync
{
    public partial class SyncValueDropdown : Window
    {

        public string SyncValueEntry { get; set; }

        public SyncValueDropdown()
        {
            InitializeComponent();
        }

        private void Save(object sender, RoutedEventArgs e)
        {
            if (dropdownentry.Text != "") { SyncValueEntry = dropdownentry.Text; this.DialogResult = true; }
            else { MessageBox.Show("Please Select A Value"); }
        }
    }
}
