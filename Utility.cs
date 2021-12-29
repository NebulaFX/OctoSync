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
        /// <summary>
        /// Asynchronously Start The Mirror Cycle
        /// </summary>
        public static void StartTheSync()
        {
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
