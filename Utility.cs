using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OctoSync
{
    class Utility
    {
        // Query Information For Sync
        public static DataTable ServerData { get; set; }
        public static DataTable ClientData { get; set; }

        /// <summary>
        /// Asynchronously Start The Mirror Cycle
        /// </summary>
        public async static void StartTheSync()
        {
            // Objects
            MainWindow mw = new();

            // Load Data Into Data Tables
            ClientData = await SQL.GetSQLData("Select [Name Of Item] ,(SELECT [Setting] FROM [Settings] Where SettingName = 'StockLocationReferenceName') as StoreCode ,[Quantity] From Stock Where [Name of Item] != '' and [Name of Item] is not Null order by Quantity desc");
            ServerData = await SQL.GetSQLData("Select [Name Of Item] ,[StoreCode] ,[Quantity] From OctoSyncStock order by Quantity desc");

            // Write Server Details
            StringBuilder sb = new StringBuilder();
            foreach (DataRow cData in ClientData.Rows)
            {
                // SERVER DATA
                string c_NameOfItem = cData["Name of Item"].ToString();
                string c_StoreCode = cData["StoreCode"].ToString();
                string c_Quantity = cData["Quantity"].ToString();

                // CLIENT DATA
                foreach (DataRow sData in ServerData.Rows)
                {
                    string s_NameOfItem = sData["Name Of Item"].ToString();
                    string s_StoreCode = sData["StoreCode"].ToString();
                    string s_Quantity = sData["Quantity"].ToString();

                    // Compare 
                    if (s_NameOfItem == c_NameOfItem && s_StoreCode == c_StoreCode && s_Quantity != c_Quantity)
                    {
                        await SQL.ExecuteThisQuery($"Update OctoSyncStock set Quantity = '{c_Quantity}' where [Name Of Item] = '{c_NameOfItem}' AND StoreCode = '{c_StoreCode}'");
                        File.AppendAllText("LOGS.txt", $"Updated Product: {c_NameOfItem}, Set Current Quantity To: {c_Quantity} Where Quantity Was {s_Quantity}" + Environment.NewLine);
                    }
                }
            }
         }

        /// <summary>
        /// Asynchronously Create The Required Tables: OctoSyncStock (String SyncValueToEnter)
        /// </summary>
        /// <returns></returns>
        public static string SyncValueForServer { get; set; }
        public static Task<Task> CreateTheRequiredTables(string SyncValueToEnter)
        {
            return Task.Run(async () =>
            {
                // Convert SyncValue To SQL
                if (SyncValueToEnter == "Name Of Item") { SyncValueForServer = "Name Of Item"; }
                if (SyncValueToEnter == "Supplier Code") { SyncValueForServer = "CodeSup"; }
                if (SyncValueToEnter == "Barcode") { SyncValueForServer = "Barcode"; }
                if (SyncValueToEnter == "Internal Reference Code") { SyncValueForServer = "InternalRefCode"; }

                await SQL.ExecuteThisQuery($"CREATE TABLE OctoSyncStock ( [{SyncValueForServer}] Nvarchar(Max), StoreCode Nvarchar(MAX), Quantity Bigint)");

                return Task.CompletedTask;
            });
        }
    }
}
