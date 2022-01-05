using System;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace OctoSync
{
    class Utility
    {
        // Properties & Constructors
        #region [** -- Properties -- **]

        // Query Information For Sync
        public static DataTable ServerData { get; set; }
        public static DataTable ClientData { get; set; }
        public static DataTable DataFromQuery { get; set; }
        public static string SyncValueForServer { get; set; }
        public static string CustomerID { get; set; } = "150";

        // Loop Information
        public static bool IsInLoop { get; set; } = true;

        // Product Loop Inforamation
        public static double TotalProductsMirrored { get; set; } = 0;
        public static double TotalProductsErrored { get; set; } = 0;
        public static double TotalProductsUpToDate { get; set; } = 0;
        #endregion

        /// 
        /// Asynchronously Start The Mirror Cycle
        ///
        #region [**] Mirror Cycle Logic

        #region [1] Convert Sync Value -> SQL Friendly

        public static void ConvertSyncValue(string SyncValue)
        {
            // Convert SyncValue To SQL
            if (SyncValue == "Name Of Item") { SyncValueForServer = $"Name Of Item"; }
            if (SyncValue == "Supplier Code") { SyncValueForServer = "CodeSup"; }
            if (SyncValue == "Barcode") { SyncValueForServer = "Barcode"; }
            if (SyncValue == "Internal Reference Code") { SyncValueForServer = "InternalRefCode"; }
        }

        #endregion

        #region [2] Actual Mirror Cycle
        public static bool AddedProduct { get; set; }

        public async static void StartTheSync(string SyncValueToEnter, string LoopMinutes = "1", string StoreCode = "")
        {
            // Collect Customer ID
            CustomerID = SQL.ExecuteSQLScalar("Select Max(CustID) from UsageToUpload where CustId != '150'");
            if (CustomerID == "") { CustomerID = "150"; }

            // Convert Loop To Minutes from MS
            int LoopMiliseconds = Convert.ToInt32(LoopMinutes) * 60000; // 40 Minutes = 2,400,000 Milliseconds

            // Convert Friendly Values
            ConvertSyncValue(SyncValueToEnter);

            // Begin Loop & Mirror Process
            while (IsInLoop)
            {
                TotalProductsMirrored = 0;
                TotalProductsErrored = 0;
                TotalProductsUpToDate = 0;
                // Stop Progress If In Active Loop
                if (IsInLoop == false) { await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" |  ---------------- * Sync Terminated * ----------------" + Environment.NewLine); await SQL.PostToServer($"---------------- * Sync Terminated * ----------------"); break; }
                await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" |  Time Until Next Sync: {LoopMinutes} Minute(s)" + Environment.NewLine);
                await SQL.PostToServer($"Time Until Next Sync: {LoopMinutes} Minute(s)");

                await Task.Delay(LoopMiliseconds);

                if (IsInLoop == false) { await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" |  ---------------- * Sync Terminated * ----------------" + Environment.NewLine); await SQL.PostToServer($"---------------- * Sync Terminated * ----------------"); break; }
                await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" |  ---------------- * Sync Started * ----------------" + Environment.NewLine);
                await SQL.PostToServer($"---------------- * Sync Started * ----------------");

                // Obtain Client Data
                ClientData = await SQL.GetSQLData_FromClient($"Select [{SyncValueForServer}] ,(SELECT [Setting] FROM [Settings] Where SettingName = 'StockLocationReferenceName') as StoreCode ,[Quantity] , Cast([{SyncValueForServer}] + ' ••• ' + (SELECT [Setting] FROM [Settings] Where SettingName = 'StockLocationReferenceName') as nvarchar(MAX)) as 'CombinedInfo' From Stock S Where HASHBYTES('MD5', S.[Name of Item]) in (select HashName from StockAdjustmentHistory3 Where [Datetime] > (DATEADD(minute, -{LoopMinutes + 2}, GETDATE())) and FieldName = '3') and [{SyncValueForServer}] != '' and [{SyncValueForServer}] is not Null");

                // '1234 - 4XL', '123 - 123'
                StringBuilder sb = new();
                sb.Clear();
                foreach (DataRow row in ClientData.Rows)
                {
                    string RowValue = row[$"{SyncValueForServer}"].ToString();
                    sb.Append($"'{RowValue}',");
                }

                // Obtain Server Data
                ServerData = await SQL.GetSQLData($"Select [{SyncValueForServer}], [StoreCode] ,[Quantity] , Cast([{SyncValueForServer}] + ' ••• ' + [StoreCode] as nvarchar(max)) as 'CombinedInfo' From OctoSyncStock Where [{SyncValueForServer}] in ({sb.ToString().TrimEnd(',')}) order by Quantity desc");

                #region [1] Start Loop -> Convert Values For Active Server

                // Check File Size, If Over 600mb Delete The File
                FileInfo fi = new FileInfo($"Logs_{StoreCode}.txt");
                if (fi.Length <= 629000000 && File.Exists($"Logs_{StoreCode}.txt)")) { File.Delete($"Logs_{StoreCode}.txt"); }
                await SQL.PostToServer($"Log File Exceeded 600mb, It Has Been Deleted");


                #endregion

                // Find Items To Update
                if (ClientData.Rows.Count > 0)
                {
                    foreach (DataRow cData in ClientData.Rows)
                    {
                        await Task.Delay(Convert.ToInt32(MainWindow.CustomSyncDelay));
                        // Obtain Client Data
                        AddedProduct = false;
                        string c_NameOfItem = cData[$"{SyncValueForServer}"].ToString();
                        string c_StoreCode = cData["StoreCode"].ToString();
                        string c_Quantity = cData["Quantity"].ToString();
                        string c_CombinedInformation = cData["CombinedInfo"].ToString();

                        // Loop & Configure Entries For Active Server
                        if (!AddedProduct)
                        {
                            #region [*] Product Needs Updating
                            foreach (DataRow sData in ServerData.Rows)
                            {
                                // Obtain Information, Create Log & Update Active Server
                                string s_NameOfItem = sData[$"{SyncValueForServer}"].ToString().Replace("'", "");
                                string s_StoreCode = sData["StoreCode"].ToString().Replace("'", "");
                                string s_Quantity = sData["Quantity"].ToString().Replace("'", "");
                                string s_CombinedInformation = sData["CombinedInfo"].ToString().Replace("'", "");

                                if (c_CombinedInformation == s_CombinedInformation && c_Quantity != s_Quantity)
                                {
                                    TotalProductsMirrored++;
                                    AddedProduct = true;
                                    await File.AppendAllTextAsync($"Logs_{c_StoreCode}.txt", DateTime.Now + $" | Updated Product (Quantity Change): {c_NameOfItem}, Set Current Quantity To: {c_Quantity} Where Quantity Was {s_Quantity}" + Environment.NewLine);
                                    await SQL.ExecuteThisQuery($"Update OctoSyncStock set [Quantity] = '{c_Quantity}' where [{SyncValueForServer}] = '{c_NameOfItem}' AND [StoreCode] = '{c_StoreCode}'");
                                    await SQL.PostToServer($"Updated Product (Quantity Change): {c_NameOfItem}, Set Current Quantity To: {c_Quantity} Where Quantity Was {s_Quantity}");
                                }
                            }
                            #endregion
                        }
                        if (!AddedProduct)
                        {
                            #region [*] Product Is Up To Date
                            foreach (DataRow sData in ServerData.Rows)
                            {
                                // Product Is Up To Date, Create A Log & Move On
                                string s_NameOfItem = sData[$"{SyncValueForServer}"].ToString().Replace("'", "");
                                string s_StoreCode = sData["StoreCode"].ToString().Replace("'", "");
                                string s_Quantity = sData["Quantity"].ToString().Replace("'", "");
                                string s_CombinedInformation = sData["CombinedInfo"].ToString().Replace("'", "");

                                if (c_CombinedInformation == s_CombinedInformation && c_Quantity == s_Quantity)
                                {
                                    TotalProductsUpToDate++;
                                    await File.AppendAllTextAsync($"Logs_{c_StoreCode}.txt", DateTime.Now + $" | Missed Product (No Change): {c_NameOfItem} (Quantity: {c_Quantity})" + Environment.NewLine);
                                    await SQL.PostToServer($"Missed Product (No Change): {c_NameOfItem} (Quantity: {c_Quantity})");
                                    AddedProduct = true;
                                }
                            }
                            #endregion
                        }
                        if (!AddedProduct)
                        {
                            TotalProductsMirrored++;
                            if (ServerData.Rows.Count > 0)
                            {
                                #region [*] Product Is New [Needs Inserting]
                                foreach (DataRow sData in ServerData.Rows)
                                {
                                    if (!AddedProduct)
                                    {
                                        // Obtain Information, Create Log & Insert Active Server
                                        string s_NameOfItem = sData[$"{SyncValueForServer}"].ToString();
                                        string s_StoreCode = sData["StoreCode"].ToString();
                                        string s_Quantity = sData["Quantity"].ToString();
                                        string s_CombinedInformation = sData["CombinedInfo"].ToString();

                                        if (c_CombinedInformation != s_CombinedInformation)
                                        {
                                            await File.AppendAllTextAsync($"Logs_{c_StoreCode}.txt", DateTime.Now + $" | Inserted Product (New Product): {c_NameOfItem} (Quantity: {c_Quantity})" + Environment.NewLine);
                                            await SQL.ExecuteThisQuery($"Insert Into [OctoSyncStock] ([{SyncValueForServer}], [StoreCode], [Quantity]) VALUES ('{c_NameOfItem}', '{c_StoreCode}', '{c_Quantity}')");
                                            await SQL.PostToServer($"Inserted Product (New Product): {c_NameOfItem} (Quantity: {c_Quantity})");
                                            AddedProduct = true;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                await File.AppendAllTextAsync($"Logs_{c_StoreCode}.txt", DateTime.Now + $" | Inserted Product (New Product): {c_NameOfItem} (Quantity: {c_Quantity})" + Environment.NewLine);
                                await SQL.ExecuteThisQuery($"Insert Into [OctoSyncStock] ([{SyncValueForServer}], [StoreCode], [Quantity]) VALUES ('{c_NameOfItem}', '{c_StoreCode}', '{c_Quantity}')");
                                await SQL.PostToServer($"Inserted Product (New Product): {c_NameOfItem} (Quantity: {c_Quantity})");
                                AddedProduct = true;
                            }
                            #endregion
                        }
                        if (!AddedProduct)
                        {
                            #region [*] Product Error, Log Created
                            await File.AppendAllTextAsync($"Logs_{c_StoreCode}.txt", DateTime.Now + $" | Missed Product (ERROR PRODUCT): {c_NameOfItem}" + Environment.NewLine);
                            await SQL.PostToServer($"Missed Product (ERROR PRODUCT): {c_NameOfItem}");
                            TotalProductsErrored++;
                            #endregion
                        }
                    }
                }
                else
                {
                    await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" |  * No Products To Mirror, Everything Up To Date *" + Environment.NewLine);
                    await SQL.PostToServer($"* No Products To Mirror, Everything Up To Date *");
                }
                await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" |  ---------------- * Sync Finished (Mirrored {TotalProductsMirrored} Products, {TotalProductsUpToDate} Checked & Up To Date, {TotalProductsErrored} Errors Found) * ----------------" + Environment.NewLine);
                await SQL.PostToServer($"---------------- * Sync Finished (Mirrored {TotalProductsMirrored} Products, {TotalProductsUpToDate} Checked & Up To Date, {TotalProductsErrored} Errors Found) * ----------------");
            }
        }
        #endregion

        #endregion

        /// 
        /// Asynchronously Create The Required Tables: OctoSyncStock (String SyncValueToEnter)
        /// 
        #region [**] Create Required OctoSync Tables
        public static Task<Task> CreateTheRequiredTables(string SyncValueToEnter)
        {
            return Task.Run(async () =>
            {
                // Convert SyncValue To SQL
                if (SyncValueToEnter == "Name Of Item") { SyncValueForServer = $"Name Of Item"; }
                if (SyncValueToEnter == "Supplier Code") { SyncValueForServer = "CodeSup"; }
                if (SyncValueToEnter == "Barcode") { SyncValueForServer = "Barcode"; }
                if (SyncValueToEnter == "Internal Reference Code") { SyncValueForServer = "InternalRefCode"; }

                // Create The Table, Insert For Initilization
                await SQL.ExecuteThisQuery($"CREATE TABLE OctoSyncStock ( [{SyncValueForServer}] Nvarchar(Max), StoreCode Nvarchar(MAX), Quantity Float)" +
                                           $"Insert Into OctoSyncStock ([{SyncValueForServer}], StoreCode, Quantity) VALUES ('PremierEPOSTestItem', 'PremierEPOSTestItem', 0)");
                await SQL.PostToServer($"No tables exist, creating required tables");

                // Handoff
                return Task.CompletedTask;
            });
        }
        #endregion

        ///
        /// Complete A Full Upload
        ///
        #region [**] Full Upload
        
        public static async Task<Task> CompleteFullUpload(string SyncValueFromMainWindow, string StoreCode, string LoggedInStaffName)
        {
            return Task.Run(async () => 
            {
                // If override is set to no
                if (MainWindow.OverrideProductsOnFullUpload == false)
                {
                    // Notify Start
                    await SQL.PostToServer($"Full Upload Operation Started (Options: Don't Overwrite Products)", LoggedInStaffName);
                    await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" |  * Full Upload Operation Started (Options: Don't Overwrite Products)*" + Environment.NewLine);

                    ConvertSyncValue(SyncValueFromMainWindow);
                    try { ClientData = await SQL.GetSQLData_FromClient($"Select [{SyncValueForServer}] ,(SELECT [Setting] FROM [Settings] Where SettingName = 'StockLocationReferenceName') as StoreCode ,[Quantity] From Stock S Where [{SyncValueForServer}] != '' and [{SyncValueForServer}] is not Null"); }
                    catch (Exception ee)
                    {
                        await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" |  Failed to obtain data from local client." + Environment.NewLine + ee.Message + Environment.NewLine);
                        await SQL.PostToServer($"Failed to obtain data from local client." + Environment.NewLine + ee.Message + Environment.NewLine, LoggedInStaffName);
                    }

                    // if the product does not exist on the server, add it
                    if (ClientData.Rows.Count > 0)
                    {
                        foreach (DataRow cData in ClientData.Rows)
                        {
                            await Task.Delay(Convert.ToInt32(MainWindow.CustomSyncDelay));
                            string c_NameOfItem = cData[$"{SyncValueForServer}"].ToString();
                            string c_StoreCode = cData["StoreCode"].ToString();
                            string c_Quantity = cData["Quantity"].ToString();

                            // Check If Product Exists On Server
                            ServerData = await SQL.GetSQLData($"Select [{SyncValueForServer}] From [OctoSyncStock] Where [{SyncValueForServer}] = '{c_NameOfItem}'");

                            if (ServerData.Rows.Count == 0)
                            {
                                // Insert Product
                                await Task.Delay(Convert.ToInt32(MainWindow.CustomSyncDelay));
                                await SQL.ExecuteThisQuery($"Insert Into [OctoSyncStock] ([{SyncValueForServer}], [StoreCode], [Quantity]) VALUES ('{c_NameOfItem}', '{c_StoreCode}', '{c_Quantity}')");
                                await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" | Inserted Product (Full Upload): {c_NameOfItem} (Quantity: {c_Quantity})" + Environment.NewLine);
                                await SQL.PostToServer($"Inserted Product (Full Upload): {c_NameOfItem} (Quantity: {c_Quantity})");
                            }
                        }
                    }
                    else
                    {
                        await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" |  * No Products To Mirror, Everything Up To Date *" + Environment.NewLine);
                        await SQL.PostToServer($"* No Products To Mirror, Everything Up To Date *");
                    }

                    // Notify Completion
                    await SQL.PostToServer($"Full Upload Operation Completed (Options: Don't Overwrite Products)", LoggedInStaffName);
                    await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" |  * Full Upload Operation Completed (Options: Don't Overwrite Products) *" + Environment.NewLine);
                }

                // override set, overwrite all products
                else
                {
                    // Notify Start
                    await SQL.PostToServer($"Full Upload Operation Started (Options: Overwrite Products)", LoggedInStaffName);
                    await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" |  * Full Upload Operation Started (Options: Overwrite Products)*" + Environment.NewLine);

                    ConvertSyncValue(SyncValueFromMainWindow);
                    try { ClientData = await SQL.GetSQLData_FromClient($"Select [{SyncValueForServer}] ,(SELECT [Setting] FROM [Settings] Where SettingName = 'StockLocationReferenceName') as StoreCode ,[Quantity] From Stock S Where [{SyncValueForServer}] != '' and [{SyncValueForServer}] is not Null"); }
                    catch (Exception ee)
                    {
                        await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" |  Failed to obtain data from local client." + Environment.NewLine + ee.Message + Environment.NewLine);
                        await SQL.PostToServer($"Failed to obtain data from local client." + Environment.NewLine + ee.Message + Environment.NewLine, LoggedInStaffName);
                    }

                    // upload all products and delete them if they already exist
                    if (ClientData.Rows.Count > 0)
                    {
                        foreach (DataRow cData in ClientData.Rows)
                        {
                            string c_NameOfItem = cData[$"{SyncValueForServer}"].ToString();
                            string c_StoreCode = cData["StoreCode"].ToString();
                            string c_Quantity = cData["Quantity"].ToString();

                            // Check If Product Exists On Server
                            ServerData = await SQL.GetSQLData($"Select [{SyncValueForServer}] From [OctoSyncStock] Where [{SyncValueForServer}] = '{c_NameOfItem}'");

                            if (ServerData.Rows.Count == 0)
                            {
                                // Insert Product
                                await Task.Delay(Convert.ToInt32(MainWindow.CustomSyncDelay));
                                await SQL.ExecuteThisQuery($"Insert Into [OctoSyncStock] ([{SyncValueForServer}], [StoreCode], [Quantity]) VALUES ('{c_NameOfItem}', '{c_StoreCode}', '{c_Quantity}')");
                                await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" | Inserted Product (Full Upload): {c_NameOfItem} (Quantity: {c_Quantity})" + Environment.NewLine);
                                await SQL.PostToServer($"Inserted Product (Full Upload): {c_NameOfItem} (Quantity: {c_Quantity})");
                            }
                            else
                            {
                                // Delete & Overwrite the product
                                await Task.Delay(Convert.ToInt32(MainWindow.CustomSyncDelay));
                                await SQL.ExecuteThisQuery($"Delete From [OctoSyncStock] Where [{SyncValueForServer}] = '{c_NameOfItem}' and [StoreCode] = '{c_StoreCode}'");
                                await SQL.ExecuteThisQuery($"Insert Into [OctoSyncStock] ([{SyncValueForServer}], [StoreCode], [Quantity]) VALUES ('{c_NameOfItem}', '{c_StoreCode}', '{c_Quantity}')");
                                await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" | Product Override (Full Upload): {c_NameOfItem} (Quantity: {c_Quantity})" + Environment.NewLine);
                                await SQL.PostToServer($"Deleted & Overwrote Product (Full Upload): {c_NameOfItem} (Quantity: {c_Quantity})");
                            }
                        }
                    }

                    // Notify Completion
                    await SQL.PostToServer($"Full Upload Operation Completed (Options: Overwrite Products)", LoggedInStaffName);
                    await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" |  * Full Upload Operation Completed (Options: Overwrite Products) *" + Environment.NewLine);
                }
            });
        }
        #endregion

    }
}
