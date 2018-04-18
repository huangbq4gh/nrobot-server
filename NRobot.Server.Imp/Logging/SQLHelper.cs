using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Configuration;
using log4net;

/// <summary>
/// This class holds insert statements for SQL Database
/// </summary>

namespace NRobot.Server.Imp.Helpers
{
    public class SQLHelper
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SQLHelper));
        private static readonly string connString =  ConfigurationManager.AppSettings["SQLServerConnectionString"];

        /// <summary>
        /// Generic SQL insert
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="columns"></param>
        /// <param name="values"></param>
        /// <returns>The first column of the first row in the result set returned by the query</returns>
        public object Insert(string tableName, string[] columns, object[] values)
        {
            object result = -1;
            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "INSERT INTO " + tableName + "(" + string.Join(", ", columns) + ") OUTPUT INSERTED.ID VALUES (@" + string.Join(", @", columns) + ")";
                        cmd.Prepare();
                        for (int i = 0; i < values.Length; i++)
                        {
                            if (values[i] == null)
                            {
                                cmd.Parameters.AddWithValue("@" + columns[i], DBNull.Value);
                            }
                            else
                            {
                                cmd.Parameters.AddWithValue("@" + columns[i], values[i]);
                            }
                        }

                        result = cmd.ExecuteScalar();
                    }

                    conn.Close();
                }
            }
            catch (SqlException e)
            {
                Log.Error(e.ToString());
            }

            return result;
        }       
    }
}
