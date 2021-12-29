using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoSync
{
    class SQL
    {
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
                try { sqlConnection.ConnectionString = MainWindow.ServerConnectionString; } catch { }

                using (SqlDataAdapter da = new SqlDataAdapter(QueryToExecute, sqlConnection.ConnectionString))
                {
                    try { da.Fill(dt); } catch { }
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
                try { sqlConnection.ConnectionString = MainWindow.ServerConnectionString; } catch { }

                using (SqlDataAdapter da = new SqlDataAdapter(QueryToExecute, sqlConnection.ConnectionString))
                {
                    try { da.Fill(dt); } catch { }
                }

                return dt;
            });
        }

        /// <summary>
        /// Asynchronously Executes A NON-Query & Returns No Result (string QueryToExecute)
        /// </summary>
        public static Task ExecuteThisQuery(string QueryToExecute)
        {
            return Task.Run(() =>
            {
                using (SqlConnection connection = new SqlConnection(MainWindow.ServerConnectionString))
                {
                    SqlCommand command = new SqlCommand(QueryToExecute, connection);
                    command.Connection.Open(); command.ExecuteNonQuery(); command.Connection.Close();
                }
            });
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
                    catch (Exception 
                    ex)
                    {
                    }
                    if (connection.State != ConnectionState.Closed)
                    {
                        connection.Close();
                    }
                    tempcommand.Dispose();
                }
                catch (Exception ex2)
                {
                    result = "";
                }
                return result;
            }
        }
    }
}
