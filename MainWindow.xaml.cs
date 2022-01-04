using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Media;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

// TODO:
// ** MINOR **
// - Assign Startup Task For Program In Windows [Configurable]

// ** MAJOR **
// - Login As SQL User, Rather Than 3141 [Log Entry To SQL]
// - Upload Errors To Server [With Customer Number]
// - Button to upload all?
// - ADD CUSTOM COOLDOWN FOR DIFFERENT PC SYSTEMS?
// - Only Show logs if Customer is 150 or 719

namespace OctoSync
{
    public partial class MainWindow : Window
    {
        // Save Information
        public bool DoesTableExist { get; set; }

        // Connection Information
        public static string LocalConnectionString { get; set; }
        public static string ServerConnectionString { get; set; }

        // Program Information
        public static bool LiveRefreshTheLogs { get; set; } = false;
        public static bool AutomaticInformationFillIn { get; set; } = false;

        // User Login Information
        public static string Username { get; set; }
        public static string Password { get; set; }

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
                    SQL.TheStoreCode = StoreCode;
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
                Properties.Settings.Default.LocalConnectionString = LocalConnectionStringBox.Text;
                Properties.Settings.Default.Save();
                Properties.Settings.Default.Reload();
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

                Properties.Settings.Default.ServerConnectionString = ServerConBox.Text;
                Properties.Settings.Default.Save();
                Properties.Settings.Default.Reload();
            }
        }
        #endregion

        // Program Functions 
        #region [*] Load The Form

        public MainWindow()
        {
            // Start The Form
            InitializeComponent();
            AddConsoleEntry("Mirror Software Started");

            // Check If Program Can Fill In Information Automatically
            if (!String.IsNullOrEmpty(Properties.Settings.Default.LocalConnectionString) && !String.IsNullOrEmpty(Properties.Settings.Default.ServerConnectionString))
            {
                // Notify Program Information Has Been Set Automatically, So Run Obtaining Methods
                AutomaticInformationFillIn = true;
                LocalConnectionStringBox.Text = Properties.Settings.Default.LocalConnectionString;
                ServerConBox.Text = Properties.Settings.Default.ServerConnectionString;
                LocalConnectionStringBox.IsEnabled = false;
                ServerConBox.IsEnabled = false;

                // Obtain Sync Value Field
                #region [*] Obtain Sync Value Field

                SqlConnection conn = new SqlConnection(ServerConBox.Text);
                SqlCommand cmd = new SqlCommand("SELECT Top(1) ISNULL(NULLIF(LTRIM(RTRIM(Column_Name)), ''), 'Not Available') as 'Temp' FROM INFORMATION_SCHEMA.COLUMNS Where TABLE_NAME = 'OctoSyncStock'", conn);
                conn.Open();

                var SyncValue_raw = cmd.ExecuteScalar(); // Might Return Null

                conn.Close();

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
                #endregion

                // Obtain Store Code Value
                #region [*] Obtain Store Code Value
                GenericDataEntry gde = new();

                try
                {
                    // Obtain Store Code Based On Local Credentials
                    #region [*] Obtain Store Code

                    SqlConnection connstring = new SqlConnection(LocalConnectionStringBox.Text);
                    SqlCommand cmdd = new SqlCommand("SELECT [Setting] FROM [Settings] Where SettingName = 'StockLocationReferenceName'", connstring);
                    connstring.Open();
                    string StoreCode = cmdd.ExecuteScalar().ToString();
                    connstring.Close();
                    StoreCodeBox.Text = StoreCode;
                    AddConsoleEntry("Store Code Found: " + StoreCode);
                    SQL.TheStoreCode = StoreCode;
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
                    #endregion
                }
                #endregion

                // Post Object To Server
                SQL.PostToServer($"Obtained Values Automatically: SYNC VALUE: {SyncValue_raw.ToString()} STORE CODE: {StoreCodeBox.Text}");
            }
        }

        private void MoveTheForm(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) { this.DragMove(); }
        }

        private void FAKE_CloseTheForm(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
        }

        private void Terminate_Click(object sender, RoutedEventArgs e)
        {
            Utility.IsInLoop = false;
            SaveBut.IsHitTestVisible = true;
            Terminate.Background = Brushes.Firebrick;
            Terminate.Foreground = Brushes.White;
            Terminate.Content = "TERMINATED";
            Terminate.FontWeight = FontWeights.Bold;
            Terminate.BorderBrush = Brushes.Firebrick;

            // UI Go For Mirror
            SaveBut.BorderBrush = (SolidColorBrush)new BrushConverter().ConvertFrom("#2f7ae5");
            SaveBut.Content = "Start Mirror";
            SaveBut.FontWeight = FontWeights.Normal;
            SaveBut.Background = Brushes.White;
            SaveBut.Foreground = Brushes.Black;

            SQL.PostToServer($"Sync Terminated", MainWindow.Username);
        }

        private void CloseProgram_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Warning: This will close the program, continue?", "Are You Sure?", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes) { Application.Current.Shutdown(); SQL.PostToServer("Program Closed", Username); }
        }

        #endregion
        #region [*] Login Form & Add Console Entry
        public async void AddConsoleEntry(string ConsoleEntry)
        {
            // Insert Into Text File
            if (StoreCodeBox.Text != "")
            {
                File.AppendAllText($"Logs_{StoreCodeBox.Text}.txt", DateTime.Now + " | " + ConsoleEntry + Environment.NewLine);
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
            SQL.PostToServer($"Cycle Minutes Updated To: " + CycleMinsBox.Text, Username);
        }
        #endregion
        #region [*] Tabs Logic

        private void SettingsTab_Click(object sender, RoutedEventArgs e)
        {
            LogsTab.Opacity = 0.5;
            ConfigurationTab.Opacity = 0.5;
            SettingsTab.Opacity = 1;
            SettingsTab.FontWeight = FontWeights.Bold;
            LogsTab.FontWeight = FontWeights.Normal;
            ConfigurationTab.FontWeight = FontWeights.Normal;
            LiveRefreshTheLogs = false;
            SettingsTabby.Visibility = Visibility.Visible;
        }

        private async void LogsTab_Click(object sender, RoutedEventArgs e)
        {
            LogsTab.Opacity = 1;
            ConfigurationTab.Opacity = 0.5;
            SettingsTab.Opacity = 0.5;
            SettingsTab.FontWeight = FontWeights.Normal;
            LogsTab.FontWeight = FontWeights.Bold;
            ConfigurationTab.FontWeight = FontWeights.Normal;
            ConfigurationTabby.Visibility = Visibility.Collapsed;
            LoggyTabby.Visibility = Visibility.Visible;
            SettingsTabby.Visibility = Visibility.Collapsed;

            // Start live refresh for logs
            LiveRefreshTheLogs = true;

            while (LiveRefreshTheLogs) 
            {
                if (!LiveRefreshTheLogs) { break; }
                await Task.Delay(250);
                if (File.Exists($"Logs_{StoreCodeBox.Text}.txt"))
                {
                    Console.Text = File.ReadAllText($"Logs_{StoreCodeBox.Text}.txt");
                    Console.ScrollToEnd();
                }
            }
        }

        private void ConfigurationTab_Click(object sender, RoutedEventArgs e)
        {
            LogsTab.Opacity = 0.5;
            ConfigurationTab.Opacity = 1;
            SettingsTab.Opacity = 0.5;
            SettingsTab.FontWeight = FontWeights.Normal;
            LogsTab.FontWeight = FontWeights.Normal;
            ConfigurationTab.FontWeight = FontWeights.Bold;
            ConfigurationTabby.Visibility = Visibility.Visible;
            LoggyTabby.Visibility = Visibility.Collapsed;
            SettingsTabby.Visibility = Visibility.Collapsed;
            LiveRefreshTheLogs = false;
        }

        #endregion
        #region [*] Login To Program

        private async void LoginButton(object sender, RoutedEventArgs e)
        {
            bool DetailsAreCorrect = SQL.CheckLoginDetails(UserBox.Text, PassBox.Password.ToString());
            if (DetailsAreCorrect) 
            { 
                // Save details & clear forms
                SignInForm.Visibility = Visibility.Collapsed;
                await File.AppendAllTextAsync($"Logs_{StoreCodeBox.Text}.txt", DateTime.Now + $" | Support User Logged In: {UserBox.Text}" + Environment.NewLine);
                Username = UserBox.Text;
                Password = PassBox.Password.ToString();
                UserBox.Text = String.Empty;
                PassBox.Password = String.Empty;
            }
            else { MessageBox.Show($"Login Failed For User: {UserBox.Text}"); }
        }

        #endregion
        #region [----------------------------------] DEBUG FUNCTION
        private void TESTBUTTON(object sender, MouseButtonEventArgs e)
        {
            File.WriteAllText($"Logs_{StoreCodeBox.Text}.txt", DateTime.Now + " | " + $"---------------- * Log File Cleared By {Username} * ----------------" + Environment.NewLine);
        }
        #endregion

        #endregion

        private async void SAVEFORM(object sender, RoutedEventArgs e)
        {
            if (StoreCodeBox.Text != "")
            {
                string _StoreCode = StoreCodeBox.Text;
                SQL.TheStoreCode = _StoreCode;

                await SQL.PostToServer($"Sync Activated Manually", MainWindow.Username);

                // Save Information
                ServerConnectionString = ServerConBox.Text;
                LocalConnectionString = LocalConnectionStringBox.Text;

                // UI Go For Mirror
                SaveBut.BorderBrush = Brushes.LawnGreen;
                SaveBut.Content = "Mirroring...";
                SaveBut.FontWeight = FontWeights.Bold;
                SaveBut.Background = Brushes.LawnGreen;
                SaveBut.Foreground = Brushes.White;

                // Stop Termination
                Utility.IsInLoop = true;
                Terminate.Background = Brushes.White;
                Terminate.Foreground = Brushes.Black;
                Terminate.Content = "Terminate";
                Terminate.FontWeight = FontWeights.Normal;
                Terminate.BorderBrush = (SolidColorBrush)new BrushConverter().ConvertFrom("#2f7ae5");

                // Check If Table Exists
                #region [*] Check Table Exists
                try
                {
                    SqlConnection conn = new SqlConnection(ServerConBox.Text);
                    SqlCommand cmd = new SqlCommand("SELECT Top(1)Table_Name FROM INFORMATION_SCHEMA.COLUMNS Where TABLE_NAME = 'OctoSyncStock'", conn);
                    conn.Open();

                    var CheckTableExists = cmd.ExecuteScalar(); // Might Return Null

                    conn.Close();


                    if (CheckTableExists.ToString() == "OctoSyncStock")
                    {
                        // Server Has Confirmed Conversation, Start Mirror Cycle [Based On Info Provided]
                        AddConsoleEntry("Server Link Established, Starting Mirror Process");
                        Utility.StartTheSync(SyncCombobox.Text, CycleMinsBox.Text, StoreCodeBox.Text);
                        SaveBut.IsHitTestVisible = false;
                    }
                }
                catch
                {
                    // Server Has Closed The Conversation, Create The Required Tables [Result Returned: NULL]
                    AddConsoleEntry("No Tables Detected: Creating Required Tables");
                    await Utility.CreateTheRequiredTables(SyncCombobox.Text);
                    await Task.Delay(3500);
                    AddConsoleEntry("Required Tables Created, Starting Mirror Process");

                    // Tables Are Created, Start The Mirror Cycle
                    Utility.StartTheSync(SyncCombobox.Text, CycleMinsBox.Text, StoreCodeBox.Text);
                    SaveBut.IsHitTestVisible = false;
                }
            }
            #endregion
        }
    }
}
