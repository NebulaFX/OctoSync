using System;
using System.Collections.Generic;
using System.Data;
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
    public partial class GenericDataEntry : Window
    {
        // Connection Information
        public string BuiltConnectionString { get; set; }
        public string _StoreCode { get; set; }

        public GenericDataEntry()
        {
            InitializeComponent();
        }

        private void TryCon(object sender, RoutedEventArgs e)
        {
            try
            {
                // Instances
                SqlConnection SqlCon = new SqlConnection();

                // Get Connection Details
                string Port = IPandPort.Text.Split(',')[1];
                string IP = IPandPort.Text.Split(',')[0];

                // Get Username & Password
                string Username = Usernamebox.Text;
                string Password = Passwordbox.Password.ToString().Trim();

                // Get DB Name
                string DBName = DBNAME.Text.Trim(); ;

                // Replace Entered Username & Password To Connection
                string BuiltCon = $"Data Source={IP},{Port}; Initial Catalog={DBName}; User ID={Username}; Password={Password}";
                SqlCon.ConnectionString = BuiltCon;

                // Try Establishing Connection
                try { SqlCon.Open(); } catch { MessageBox.Show($"Login Failed For User: {Username}"); }

                // Open Main Menu If Connection Established
                if (SqlCon.State == ConnectionState.Open)
                {
                    SqlCon.Close();
                    MessageBox.Show("Connection Established");
                    BuiltConnectionString = BuiltCon;
                    SaveBut.IsEnabled = true;
                }
                else { MessageBox.Show("Connection Failed"); }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void SAVE(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
