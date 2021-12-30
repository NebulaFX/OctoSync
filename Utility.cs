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

        // Loop Information
        public static bool IsInLoop { get; set; } = true;

        /// <summary>
        /// Asynchronously Start The Mirror Cycle
        /// </summary>
        public static bool AddedProduct { get; set; }
        public async static void StartTheSync(string SyncValueToEnter, string LoopTime = "900000", string StoreCode = "")
        {
            while (IsInLoop)
            {
                // Stop Progress If In Active Loop
                if (IsInLoop == false) { break; await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", Environment.NewLine + DateTime.Now + $" |  Sync Paused"); }

                // Wait Specified Time
                await Task.Delay(Convert.ToInt32(LoopTime) * 60000);
                await File.AppendAllTextAsync($"Logs_{StoreCode}.txt", DateTime.Now + $" |  Sync Started" + Environment.NewLine);

                // Convert SyncValue To SQL
                if (SyncValueToEnter == "Name Of Item") { SyncValueForServer = $"Name Of Item"; }
                if (SyncValueToEnter == "Supplier Code") { SyncValueForServer = "CodeSup"; }
                if (SyncValueToEnter == "Barcode") { SyncValueForServer = "Barcode"; }
                if (SyncValueToEnter == "Internal Reference Code") { SyncValueForServer = "InternalRefCode"; }

                // Load Data Into Data Tables
                ClientData = await SQL.GetSQLData_FromClient($"Select [{SyncValueForServer}] ,(SELECT [Setting] FROM [Settings] Where SettingName = 'StockLocationReferenceName') as StoreCode ,[Quantity], Cast([{SyncValueForServer}] + ' ••• ' + (SELECT [Setting] FROM [Settings] Where SettingName = 'StockLocationReferenceName') as nvarchar(MAX)) as 'CombinedInfo' From Stock Where [{SyncValueForServer}] != '' and [{SyncValueForServer}] is not Null order by Quantity desc");
                ServerData = await SQL.GetSQLData($"Select [{SyncValueForServer}], [StoreCode] ,[Quantity], Cast([{SyncValueForServer}] + ' ••• ' + [StoreCode] as nvarchar(max)) as 'CombinedInfo' From OctoSyncStock order by Quantity desc");

                // Find Items To Update
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
                            string s_NameOfItem = sData[$"{SyncValueForServer}"].ToString();
                            string s_StoreCode = sData["StoreCode"].ToString();
                            string s_Quantity = sData["Quantity"].ToString();
                            string s_CombinedInformation = sData["CombinedInfo"].ToString();

                            if (c_CombinedInformation == s_CombinedInformation && c_Quantity != s_Quantity)
                            {
                                AddedProduct = true;
                                await File.AppendAllTextAsync($"Logs_{c_StoreCode}.txt", DateTime.Now + $" | Updated Product (Quantity Change): {c_NameOfItem}, Set Current Quantity To: {c_Quantity} Where Quantity Was {s_Quantity}" + Environment.NewLine);
                                await SQL.ExecuteThisQuery($"Update OctoSyncStock set [Quantity] = '{c_Quantity}' where [{SyncValueForServer}] = '{c_NameOfItem}' AND [StoreCode] = '{c_StoreCode}'");
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
                            string s_NameOfItem = sData[$"{SyncValueForServer}"].ToString();
                            string s_StoreCode = sData["StoreCode"].ToString();
                            string s_Quantity = sData["Quantity"].ToString();
                            string s_CombinedInformation = sData["CombinedInfo"].ToString();

                            if (c_CombinedInformation == s_CombinedInformation && c_Quantity == s_Quantity)
                            {
                                await File.AppendAllTextAsync($"Logs_{c_StoreCode}.txt", DateTime.Now + $" | Missed Product (No Change): {c_NameOfItem} (Quantity: {c_Quantity})" + Environment.NewLine);
                                AddedProduct = true;
                            }
                        }
                        #endregion
                    }
                    if (!AddedProduct)
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
                                    await SQL.ExecuteThisQuery($"Insert Into \"OctoSyncStock\" ([{SyncValueForServer}], [StoreCode], [Quantity]) VALUES ('{c_NameOfItem}', '{c_StoreCode}', '{c_Quantity}')");
                                    AddedProduct = true;
                                }
                            }
                        }
                        #endregion
                    }
                    if (!AddedProduct)
                    {
                        #region [*] Product Error, Log Created
                        await File.AppendAllTextAsync($"Logs_{c_StoreCode}.txt", DateTime.Now + $" | Missed Product (ERROR PRODUCT): {c_NameOfItem}" + Environment.NewLine);
                        #endregion
                    }
                }
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

                // Handoff
                return Task.CompletedTask;
            });
        }
    }
}
