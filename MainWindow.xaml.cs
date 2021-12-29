using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

// TODO:
// ** MINOR **
// - Error Logs Being Overwritten By Sync Status Codes
// - Log To The Console When Sync Value Is Selected
// - Save Connection Details For Re-Opening
// - Assign Startup Task For Program In Windows [Configurable]
// - Show how long until next mirror cycle
// 
// ** MAJOR **
// - Put into cycle, taking into account cycle mins
// - Insert Commands [New Products]
// - Login As SQL User, Rather Than 3141 [Log Entry To SQL]
// - Upload Errors To Server [With Customer Number]
// - Pause/Play Button
// - Force Update Button

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
        #region [*] Local Connection Settings
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
                    AddConsoleEntry("Local Connection String Added: " + gde.BuiltConnectionString);

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
        #endregion

        // Server Connection Strings
        #region [*] Server Connection Settings
        private async void ServerConnectionDetails(object sender, RoutedEventArgs e)
        {
            GenericDataEntry gde = new();
            gde.ShowDialog();

            if (gde.DialogResult == true)
            {
                try
                {
                    ServerConBox.Text = gde.BuiltConnectionString;
                    AddConsoleEntry("Server Connection String Added: " + gde.BuiltConnectionString);

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
                        AddConsoleEntry("Sync Value Entered: " + SyncValue_raw.ToString());
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
                            AddConsoleEntry("Sync Value Entered: " + SCDropdown.SyncValueEntry);
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show("Failed to load Server Connection Details" + ex.Message); }


            }
        }
        #endregion

        // Program Functions 
        #region [*] Load The Form
        public MainWindow()
        {
            InitializeComponent();
            AddConsoleEntry("Mirror Software Started");
        }
        #endregion
        #region [*] Login Form & Add Console Entry
        public async void AddConsoleEntry(string ConsoleEntry)
        {
            // Grab Entry
            Console.Text += DateTime.Now + " | " + ConsoleEntry + Environment.NewLine;

            // Insert Into Text File
            File.AppendAllText("LOGS.txt", DateTime.Now + " | " + ConsoleEntry + Environment.NewLine);
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
        #region [*] Show/Hide Form
        private void ShowFormAgain(object sender, RoutedEventArgs e)
        {
            if (this.Visibility == Visibility.Collapsed) { this.Visibility = Visibility.Visible; }
            else { this.Visibility = Visibility.Collapsed; }
        }
        #endregion
        #region [*] Show Error Log
        private void ShowErrorLog(object sender, RoutedEventArgs e)
        {
            if (File.Exists("LOGS.txt")) { File.OpenText("LOGS.txt"); }
        }
        #endregion
        #region [*] Update Cycle Minutes
        private void UpdateCycleMins(object sender, RoutedEventArgs e)
        {
            AddConsoleEntry("Cycle Minutes Updated To: " + CycleMinsBox.Text);
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
                AddConsoleEntry("No Server Link, Creating Required Tables");
                await Utility.CreateTheRequiredTables(SyncCombobox.Text);
                await Task.Delay(3500);
                AddConsoleEntry("Required Tables Created, Starting Mirror Process");

                // Tables Are Created, Start The Mirror Cycle
                Utility.StartTheSync();
            }
        }
    }
}
