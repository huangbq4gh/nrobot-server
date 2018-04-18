using System;
using System.Collections.Generic;
using NRobot.Server.Imp.Helpers;
using NRobot.Server.Imp.Domain;
using log4net;
using System.Net.Sockets;
using System.Net;

/// <summary>
/// This class deals with the logix associated with prepping database insert statements 
/// before handing them off to the SQLHelper for the actual insertion of data
/// </summary>

namespace NRobot.Server.Imp.Logging
{
    public static class ExecutionLogger
    {
        // constants
        private static Dictionary<string, string> keywords = new Dictionary<string, string>();
        private static string currentBuild;

        // logs
        private static readonly ILog Log = LogManager.GetLogger(typeof(ExecutionLogger));

        private static SQLHelper GetSQLHelper()
        {
            return new SQLHelper();
        }

        /// <summary>
        /// Sets build number in database
        /// </summary>
        public static void SetBuild(string port)
        {
            SQLHelper sql = GetSQLHelper();

            // get local IP address
            string localIp = string.Empty;
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach(var ip in host.AddressList)
            {
                if(ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIp = ip.ToString();
                }
            }
                       
            string[] columns = new string[] { "StartDate", "ip_address", "port" };
            string[] values = new string[] { DateTime.Now.ToString(), localIp, port };
            currentBuild = sql.Insert("Build", columns, values).ToString();
            Log.Debug("Logging for build=" + currentBuild + "ip address=" + localIp + " listening on port=" + port);
        }

        /// <summary>
        /// Logs an execution of a keyword and its parameters in the database.
        /// Calls methods that filter 
        /// </summary>
        /// <param name="keyword"></param>
        /// <param name="library"></param>
        /// <param name="keywordParams"></param>
        /// <param name="paramNames"></param>
        /// <param name="result"></param>
        public static void LogKeywordExecution(string keyword, string library, List<object> keywordParams, List<string> paramNames, RunKeywordResult result)
        {
            SQLHelper sql = GetSQLHelper();

            // Remove data we should not be storing (like passwords)
            FilterParams(ref keywordParams, paramNames);

            // Key = LIBRARY.KEYWORD_NAME
            string key = (library.Substring(library.IndexOf('.') +1) + "." + keyword.Replace(' ', '_')).ToUpper();

            // Add Keyword Execution to  KeywordExecution table 
            string[] columns = new string[] { "KEYWORDID", "RETURNVALUE", "DURATION", "RESULT", "FAILMESSAGE", "EXECUTIONDATE" };
            string[] values = new string[] {keywords[key], (string)result.KeywordReturn , result.KeywordDuration.ToString(), result.KeywordStatus.ToString(), result.KeywordError, DateTime.Now.ToString() };

            // Returns ID of inserted execution
            string executionID = sql.Insert("_KeywordExecution", columns, values).ToString();

            // Log parameters      
            for (int i = 0; i < paramNames.Count; i++)
            {
                string[] _columns = new string[] { "ExecutionID","OrderID", "Value" };
                string[] _values = new string[] { executionID, (i+1).ToString(), (string)keywordParams[i] };
                sql.Insert("ExecutionParameters", _columns, _values);
            }
           
        }

        /// <summary>
        /// Insert loaded keywords into database
        /// </summary>
        /// <param name="_keywords"></param>
        public static void LoadKeywords( List<Keyword> _keywords)
        {
            SQLHelper sql = GetSQLHelper();

            // Log keywords from build in DB
            foreach (Keyword k in _keywords)
            {
                string library = k.ClassInstance.ToString().Substring(k.ClassInstance.ToString().IndexOf(".") + 1);

                string [] columns = new string[] { "BuildID", "Library", "Name" };
                string [] values = new string[] { currentBuild.ToString() ,library, k.FriendlyName };
                string id = sql.Insert("_Keyword", columns, values).ToString();

                // Add keyword to dictionary
                // Concatanate Class instance and Friendly Name so  we avoid errors when keyword names across
                // libraries overlap
                // key = Library.KEYWORD_NAME
                string key = k.ClassInstance.ToString().Substring(k.ClassInstance.ToString().IndexOf(".")+1) + "." + k.FriendlyName.Trim().Replace(' ', '_');
                keywords.Add(key.ToUpper(), id);                

                // Log parameters from each keyword in DB
                string[] _columns = new string[] { "KeywordID", "OrderID", "Name" };
                for(int i = 0; i < k.ArgumentNames.Length; i ++)
                {
                    string[] _values = new string[] { id.ToString(), (i+1).ToString(), k.ArgumentNames[i] };
                    sql.Insert("KeywordParameters", _columns, _values);
                }
            }
        }

        /// <summary>
        /// Method used to filter out parameters that shouldn't be stored
        /// </summary>
        /// <param name="keywordParams"></param>
        /// <param name="paramNames"></param>
        private static void FilterParams(ref List<object> keywordParams, List<string> paramNames)
        {
            for (int i = 0; i < paramNames.Count; i++)
            {
                if (paramNames[i].ToLower().Contains("password"))
                {
                    keywordParams[i] = null;
                }
            }
        }
    }
}
