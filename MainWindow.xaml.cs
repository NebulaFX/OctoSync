using System;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace OctoSync
{
    public partial class MainWindow : Window
    {
        // Save Information
        public bool DoesTableExist { get; set; }

        // Connection Information
        public static string LocalConnectionString { get; set; }
        public static string ServerConnectionString { get; set; }

        #region [*] Load Core Resources

        // Local Connection Strings
        private void LoginDetailsWindow(object sender, RoutedEventArgs e)
        {
            GenericDataEntry gde = new();
            gde.ShowDialog();

            if (gde.DialogResult == true)
            {
                try
                {
                    LocalConnectionStringBox.Text = gde.BuiltConnectionString;
                    LocalConnectionString = gde.BuiltConnectionString;

                    // Obtain Store Code Based On Local Credentials
                    #region [*] Obtain Store Code

                    SqlConnection conn = new SqlConnection(LocalConnectionStringBox.Text);
                    SqlCommand cmd = new SqlCommand("SELECT [Setting] FROM [Settings] Where SettingName = 'StockLocationReferenceName'", conn);
                    conn.Open();
                    string StoreCode = cmd.ExecuteScalar().ToString();
                    conn.Close();
                    StoreCodeBox.Text = StoreCode;
                    AddConsoleEntry("Store Code Found: " + StoreCode);
                }
                catch (Exception ex) 
                {
                    // Notify UI
                    AddConsoleEntry("Failed To Find Store Code, Please Enter A Store Code");

                    // Show Dialog
                    UpdateStoreCode usc = new();
                    usc.ShowDialog();

                    // Get Result
                    if (usc.DialogResult == true)
                    {
                        StoreCodeBox.Text = usc.NewStoreCode;
                        AddConsoleEntry("Store Code Manually Entered: " + usc.NewStoreCode);
                    }
                }

                #endregion
            }
        }

        // Server Connection Strings
        private async void ServerConnectionDetails(object sender, RoutedEventArgs e)
        {
            GenericDataEntry gde = new();
            gde.ShowDialog();

            if (gde.DialogResult == true)
            {
                try
                {
                    ServerConBox.Text = gde.BuiltConnectionString;

                    // Obtain Sync Value Field Based On Server Credentials
                    #region [*] Obtain Sync Value Field

                    SqlConnection conn = new SqlConnection(ServerConBox.Text);
                    SqlCommand cmd = new SqlCommand("SELECT Top(1) ISNULL(NULLIF(LTRIM(RTRIM(Column_Name)), ''), 'Not Available') as 'Temp' FROM INFORMATION_SCHEMA.COLUMNS Where TABLE_NAME = 'OctoSyncStock'", conn);
                    conn.Open();

                    var SyncValue_raw = cmd.ExecuteScalar(); // Might Return Null

                    conn.Close();
                    #endregion

                    // Check The Value Does Not Return A Null
                    if (SyncValue_raw != null)
                    {
                        // Convert Values To Friendly For UI
                        if (SyncValue_raw.ToString() == "Name Of Item") { SyncCombobox.Text = "Name Of Item"; SyncCombobox.IsEnabled = false; }
                        if (SyncValue_raw.ToString() == "CodeSup") { SyncCombobox.Text = "Supplier Code"; SyncCombobox.IsEnabled = false; }
                        if (SyncValue_raw.ToString() == "Barcode") { SyncCombobox.Text = "Barcode"; SyncCombobox.IsEnabled = false; }
                        if (SyncValue_raw.ToString() == "InternalRefCode") { SyncCombobox.Text = "Internal Reference Code"; SyncCombobox.IsEnabled = false; }
                    }

                    // Value From SQL Returned A Null, Create The Required Tables
                    else 
                    {
                        // Notify UI No Tables Exist
                        AddConsoleEntry("Server Returned A Null, Creating Tables");

                        // Show DropDown Dialog To Select A Sync Value
                        SyncValueDropdown SCDropdown = new();
                        SCDropdown.ShowDialog();

                        if (SCDropdown.DialogResult == true)
                        {
                            AddConsoleEntry("Sync Value Selected: " + SCDropdown.SyncValueEntry);
                            SyncCombobox.Text = SCDropdown.SyncValueEntry;
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show("Failed to load Server Connection Details" + ex.Message); }


            }
        }

        #region [*] Load The Form
        public MainWindow()
        {
            InitializeComponent();
        }
        #endregion
        #region [*] Login Form & Add Console Entry
        public async void AddConsoleEntry(string ConsoleEntry)
        {
            Console.Text += DateTime.Now + " |  " + ConsoleEntry + Environment.NewLine;
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Check Password
                if (password.Password.ToString() == "31415926535")
                {
                    password.Visibility = Visibility.Collapsed;
                }
                else { MessageBox.Show("Wrong Password", "Wrong Password", MessageBoxButton.OK); }
            }
        }
        #endregion
        #region [*] Only allow numbers in Sync Time
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
        #endregion
        #region [*] Save Store Code
        private void SaveStoreCode(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.StoreCode = StoreCodeBox.Text;
            Properties.Settings.Default.Save();
            Properties.Settings.Default.Reload();
        }
        #endregion

        #endregion

        private async void SAVEFORM(object sender, RoutedEventArgs e)
        {
            // Save Information
            ServerConnectionString = ServerConBox.Text;
            LocalConnectionString = LocalConnectionStringBox.Text;

            // Check If Table Exists
            try 
            { 
                // Obtain Sync Value Field Based On Server Credentials
                #region [*] Obtain Sync Value Field

                SqlConnection conn = new SqlConnection(ServerConBox.Text);
                SqlCommand cmd = new SqlCommand("SELECT Top(1)Table_Name FROM INFORMATION_SCHEMA.COLUMNS Where TABLE_NAME = 'OctoSyncStock'", conn);
                conn.Open();

                var CheckTableExists = cmd.ExecuteScalar(); // Might Return Null

                conn.Close();
                #endregion

                if (CheckTableExists.ToString() == "OctoSyncStock")
                {
                    // Server Has Confirmed Conversation, Start Mirror Cycle [Based On Info Provided]
                    AddConsoleEntry("Server Link Established, Starting Mirror Process");
                    Utility.StartTheSync();
                }
            }
            catch 
            {
                // Server Has Closed The Conversation, Create The Required Tables [Result Returned: NULL]
                AddConsoleEntry("Server Link Killed, Creating Required Tables");
                await Utility.CreateTheRequiredTables(SyncCombobox.Text);
                await Task.Delay(3500);
                AddConsoleEntry("Required Tables Created, Starting Mirror Process");

                // Tables Are Created, Start The Mirror Cycle
                Utility.StartTheSync();
            }
        }
    }
}
