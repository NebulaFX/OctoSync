using System;
using System.Collections.Generic;
using System.Data.SqlClient;
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
    public partial class UpdateStoreCode : Window
    {
        public string NewStoreCode { get; set; }

        public UpdateStoreCode()
        {
            InitializeComponent();
        }

        private async void save(object sender, RoutedEventArgs e)
        {
            if (textboxentry.Text != "")
            {
                using (SqlConnection connection = new SqlConnection(MainWindow.LocalConnectionString))
                {
                    SqlCommand command = new SqlCommand($"Insert Into [Settings] (SettingName, Setting) VALUES ('StockLocationReferenceName', '{textboxentry.Text}')", connection);
                    command.Connection.Open(); command.ExecuteNonQuery(); command.Connection.Close();
                }
                NewStoreCode = textboxentry.Text;
                await SQL.PostToServer($"Store Code Entered Manually: {NewStoreCode}", MainWindow.Username);
                this.DialogResult = true;
            }
            else { MessageBox.Show("Please Enter A Store Code"); }
        }
    }
}
