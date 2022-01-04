using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace OctoSync
{
    class Utility
    {
        // Query Information For Sync
        public static DataTable ServerData { get; set; }
        public static DataTable ClientData { get; set; }
        public static string SyncValueForServer { get; set; }
        public static string CustomerID { get; set; } = "150";

        // Loop Information
        public static bool IsInLoop { get; set; } = true;

        /// <summary>
        /// Asynchronously Start The Mirror Cycle
        /// </summary>
        public static bool AddedProduct { get; set; }
        public async static void StartTheSync(string SyncValueToEnter, string LoopMinutes = "1", string StoreCode = "")
        {
            // Collect Customer ID
            CustomerID = SQL.ExecuteSQLScalar("Select Max(CustID) from UsageToUpload where CustId != '150'");
            if (CustomerID == "") { CustomerID = "150"; }

            // Convert Loop To Minutes from MS
            int LoopMiliseconds = Convert.ToInt32(LoopMinutes) * 60000; // 40 Minutes = 2,400,000 Milliseconds

            // Convert SyncValue To SQL
            if (SyncValueToEnter == "Name Of Item") { SyncValueForServer = $"Name Of Item"; }
            if (SyncValueToEnter == "Supplier Code") { SyncValueForServer = "CodeSup"; }
            if (SyncValueToEnter == "Barcode") { SyncValueForServer = "Barcode"; }
            if (SyncValueToEnter == "Internal Reference Code") { SyncValueForServer = "InternalRefCode"; }

            // Begin Loop & Mirror Process
            while (IsInLoop)
            {
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
                                    await File.AppendAllTextAsync($"Logs_{c_StoreCode}.txt", DateTime.Now + $" | Missed Product (No Change): {c_NameOfItem} (Quantity: {c_Quantity})" + Environment.NewLine);
                                    await SQL.PostToServer($"Missed Product (No Change): {c_NameOfItem} (Quantity: {c_Quantity})");
                                    AddedProduct = true;
                                }
                            }
                            #endregion
                        }
                        if (!AddedProduct)
                        {
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
                                            await SQL.PostToServer($"Insert Into [OctoSyncStock] ([{SyncValueForServer}], [StoreCode], [Quantity]) VALUES ('{c_NameOfItem}', '{c_StoreCode}', '{c_Quantity}')", "SUPPORT_STAFF_DEBUG");
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
                            #endregion
                        }
                        await Task.Delay(150);
                    }
                }
                else
                {
                    await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" |  * No Products To Mirror, Everything Up To Date *" + Environment.NewLine);
                    await SQL.PostToServer($"* No Products To Mirror, Everything Up To Date *");
                }
                await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" |  ---------------- * Sync Finished (Mirrored {ClientData.Rows.Count} Products) * ----------------" + Environment.NewLine);
                await SQL.PostToServer($"---------------- * Sync Finished (Mirrored {ClientData.Rows.Count} Products) * ----------------");
            }
        }

        /// <summary>
        /// Asynchronously Create The Required Tables: OctoSyncStock (String SyncValueToEnter)
        /// </summary>
        /// <returns></returns>
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
    }
}
