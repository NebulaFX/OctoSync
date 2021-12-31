using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoSync
{
    class SQL
    {
        public static string TheStoreCode { get; set; }

        /// <summary>
        /// Asynchronously Executes A Query & Returns The Result In A Datatable (string QueryToExecute)
        /// </summary>
        public static Task<DataTable> GetSQLData(string QueryToExecute)
        {
            return Task.Run(() => {

                // Instances
                DataTable dt = new DataTable();
                SqlConnection sqlConnection = new SqlConnection();

                // Run Query & Return As DataTable
                try { sqlConnection.ConnectionString = MainWindow.ServerConnectionString; } catch (Exception eee) { File.AppendAllText($"Logs_{TheStoreCode}", DateTime.Now + " | " + eee.Message + Environment.NewLine); }

                using (SqlDataAdapter da = new SqlDataAdapter(QueryToExecute, sqlConnection.ConnectionString))
                {
                    try { da.Fill(dt); } catch {}
                }

                return dt;
            });
        }

        public static Task<DataTable> GetSQLData_FromClient(string QueryToExecute)
        {
            return Task.Run(() => {

                // Instances
                DataTable dt = new DataTable();
                SqlConnection sqlConnection = new SqlConnection();

            // Run Query & Return As DataTable
            try { sqlConnection.ConnectionString = MainWindow.LocalConnectionString; } catch (Exception ee) { File.AppendAllText($"Logs_{TheStoreCode}", DateTime.Now + " | " + ee.Message + Environment.NewLine); }

                using (SqlDataAdapter da = new SqlDataAdapter(QueryToExecute, sqlConnection.ConnectionString))
                {
                    try { da.Fill(dt); } catch (Exception eee) { File.AppendAllText($"Logs_{TheStoreCode}.txt", DateTime.Now + " | ** SQL FAILURE ** " + eee.Message + Environment.NewLine); }
                }

                return dt;
            });
        }

        public static Task<DataTable> GetSQLData_Authorised(string QueryToExecute)
        {
            return Task.Run(() => {

                // Instances
                DataTable dt = new DataTable();
                SqlConnection sqlConnection = new SqlConnection();

                // Run Query & Return As DataTable
                try { sqlConnection.ConnectionString = MainWindow.ServerConnectionString; } catch (Exception eee) { File.AppendAllText($"Logs_{TheStoreCode}", DateTime.Now + " | " + eee.Message); }

                using (SqlDataAdapter da = new SqlDataAdapter(QueryToExecute, sqlConnection.ConnectionString))
                {
                    try { da.Fill(dt); } catch (Exception eeee) { File.AppendAllText($"Logs_{TheStoreCode}.txt", DateTime.Now + " | ** SQL FAILURE ** " + eeee.Message + Environment.NewLine); }
                }

                return dt;
            });
        }

        /// <summary>
        /// Asynchronously Executes A NON-Query & Returns No Result (string QueryToExecute)
        /// </summary>
        public static Task ExecuteThisQuery(string QueryToExecute, string StoreCode = "NO_STORE_CODE")
        {
            return Task.Run(() =>
            {
                try
                {
                    using (SqlConnection connection = new SqlConnection(MainWindow.ServerConnectionString))
                    {
                        SqlCommand command = new SqlCommand(QueryToExecute, connection);
                        command.Connection.Open(); command.ExecuteNonQuery(); command.Connection.Close();
                    }
                }
                catch (Exception ee) 
                { 
                    File.AppendAllText($"Logs_{TheStoreCode}.txt", DateTime.Now + " | ** SQL FAILURE ** " + ee.Message + Environment.NewLine);
                }
            });
        }

        /// <summary>
        /// Post an SQL Object (ActionEntry) To The Active 161 Server
        /// </summary>
        /// <param name="StaffName"></param>
        /// <param name="ActionEntry"></param>
        /// <returns></returns>
        public static Task PostToServer(string ActionEntry,string ActionStaff = "Automatic")
        {
            return Task.Run(() =>
            {
                try
                {
                    using (SqlConnection connection = new SqlConnection(Configurations.DefaultConnectionString))
                    {
                        string CleanData = ActionEntry.Replace("'", "").Replace("--", "");
                        String ObjectToSend = $"Insert Into [Services].[dbo].[OctoSyncLog] ([UUID], [CustomerNumber], [Action], [ActionStaff], [ActionDate])" +
                                              $"VALUES (NEWID(), '{Utility.CustomerID}', '{CleanData}', '{ActionStaff.ToUpper()}', GETDATE())";
                        SqlCommand command = new SqlCommand(ObjectToSend, connection);
                        command.Connection.Open(); command.ExecuteNonQuery(); command.Connection.Close();
                    }
                }
                catch (Exception ee) { File.AppendAllText($"Logs_{TheStoreCode}.txt", DateTime.Now + " | ** SQL FAILURE ** Failed To Connect To 161 Server, Local Log Created" + Environment.NewLine + ee.Message); }
            });
        }

        /// <summary>
        /// Post SQL to the 161 server
        /// </summary>
        /// <param name="QueryToExecute"></param>
        /// <param name="StoreCode"></param>
        /// <returns></returns>
        public static Task ExecuteThisQuery_161Server(string QueryToExecute)
        {
            return Task.Run(() =>
            {
                try
                {
                    using (SqlConnection connection = new SqlConnection(MainWindow.ServerConnectionString))
                    {
                        SqlCommand command = new SqlCommand(QueryToExecute, connection);
                        command.Connection.Open(); command.ExecuteNonQuery(); command.Connection.Close();
                    }
                }
                catch (Exception ee)
                {
                    File.AppendAllText($"Logs_{TheStoreCode}.txt", DateTime.Now + " | ** SQL FAILURE ** " + ee.Message + Environment.NewLine);
                }
            });
        }

        /// <summary>
        /// Sends Users Login To Active Server (string Username, string Password)
        /// and returns True if they are correct.
        /// </summary>
        public static bool CheckLoginDetails(string Username, string Password)
        {
            try
            {
                Configurations.DefaultConnectionString = Configurations.DefaultConnectionString.Replace("@", Username).Replace("^", Password);

                // Disposable SQL Instance
                using SqlConnection sql = new(Configurations.DefaultConnectionString);

                // Try Opening Connection
                sql.Open();

                // If Connection Confirmed Open, Send True
                if (sql.State == System.Data.ConnectionState.Open)
                {
                    // Close & Return Result
                    sql.Close(); 
                    return true;
                }
                else { return false; }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Asynchronously Executes A Query & Returns The Result As Scalar Obj (string QueryToExecute)
        /// </summary>
        public static string ExecuteSQLScalar(string QueryToExecute)
        {
            using (SqlConnection connection = new SqlConnection(MainWindow.ServerConnectionString))
            {
                SqlCommand tempcommand = new SqlCommand(QueryToExecute, connection);
                string result = "";
                try
                {
                    if (tempcommand.Connection.State != ConnectionState.Open)
                    {
                        tempcommand.Connection.Open();
                    }
                    tempcommand.CommandTimeout = 0;
                    try
                    {
                        result = Convert.ToString(tempcommand.ExecuteScalar());
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText($"Logs_{TheStoreCode}.txt", DateTime.Now + " | ** SQL FAILURE ** " + ex.Message + Environment.NewLine);
                    }
                    if (connection.State != ConnectionState.Closed)
                    {
                        connection.Close();
                    }
                    tempcommand.Dispose();
                }
                catch (Exception ex2)
                {
                    File.AppendAllText($"Logs_{TheStoreCode}.txt", DateTime.Now + " | ** SQL FAILURE ** " + ex2.Message + Environment.NewLine);
                    result = "";
                }
                return result;
            }
        }
    }
}
