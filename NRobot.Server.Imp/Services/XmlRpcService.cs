using System;
using System.Net;﻿  ﻿
using CookComputing.XmlRpc;
using log4net;
using NRobot.Server.Imp.Domain;
using NRobot.Server.Imp.Helpers;
using System.Collections.Concurrent;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Configuration;
using System.Data.SqlClient;
using NRobot.Server.Imp.Logging;

namespace NRobot.Server.Imp.Services
{

	/// <summary>
﻿  	/// Class of XML-RPC methods for remote library (server)
﻿  	/// that conforms to RobotFramework remote library API
﻿  	/// </summary>
﻿ 	public class XmlRpcService : XmlRpcListenerService, IRemoteService
﻿  	{
﻿  ﻿  	
		//log4net
		private static readonly ILog Log = LogManager.GetLogger(typeof(XmlRpcService));
		
		//constants
		private const String CIntro = "__INTRO__";
		private const String CInit = "__INIT__";

        //properties
	    private KeywordManager _keywordManager;
	    private ConcurrentDictionary<int, string> _threadkeywordtype;

        //constructor
	    public XmlRpcService(KeywordManager keywordManager)
	    {
	        _keywordManager = keywordManager;
            _threadkeywordtype = new ConcurrentDictionary<int, string>();
	    }


        /// <summary>
        /// Process xmlrpc request
        /// </summary>
        public override void ProcessRequest(HttpListenerContext requestContext)
        {
            //record url of request into property
            var id = Thread.CurrentThread.ManagedThreadId;
            var seg = requestContext.Request.Url.Segments;
            var typeurl = String.Join("", seg, 1, seg.Length - 1).Replace("/", ".");
            if (!_threadkeywordtype.TryAdd(id, typeurl))
            {
                throw new Exception(String.Format("Thread id {0} is already processing a request", id));
            }
            //process request
            base.ProcessRequest(requestContext);
            //remove thread property
            _threadkeywordtype.TryRemove(id, out typeurl);
        }


#region XmlRpcMethods

        /// <summary>
	﻿  ﻿  /// Get a list of keywords available for use
	﻿  ﻿  /// </summary>
	﻿  ﻿  public string[] get_keyword_names()
	  ﻿  ﻿{
	﻿  ﻿  ﻿	try 
			{
                Log.Debug("XmlRpc Method call - get_keyword_names");
			    var typename = _threadkeywordtype[Thread.CurrentThread.ManagedThreadId];
			    return _keywordManager.GetKeywordNamesForType(typename);
			}
			catch (Exception e)
			{
				Log.Error(String.Format("Exception in method - get_keyword_names : {0}",e.Message));
				throw new XmlRpcFaultException(1,e.Message);
			}
	﻿  ﻿  }
﻿  ﻿  
	﻿  ﻿  /// <summary>
	﻿  ﻿  /// Run specified Robot Framework keyword
	﻿  ﻿  /// </summary>
	﻿  ﻿  public XmlRpcStruct run_keyword(string keyword, object[] args)
	  ﻿  ﻿{
			Log.Debug(String.Format("XmlRpc Method call - run_keyword {0}",keyword));
			XmlRpcStruct kr = new XmlRpcStruct();
			try
			{
			    var typename = _threadkeywordtype[Thread.CurrentThread.ManagedThreadId];
                var result = _keywordManager.RunKeyword(typename, keyword, args);
				Log.Debug(result.ToString());
             

                // 1. Process the output string. Remove logs that not on current thread.
                // The line separator is "\r\n", so split the string by '\n'
                List<string> resultOutputList = new List<string>(((RunKeywordResult)result).KeywordOutput.Split('\n'));
                
                //1.1. Remove the extra empty array produced by split.
                if (resultOutputList.Count > 0 && resultOutputList[resultOutputList.Count - 1].Length == 0)
                {
                    resultOutputList.RemoveAt(resultOutputList.Count - 1);
                }

                int i = 0;
                
                while (i < resultOutputList.Count)
                {
                    // 1.2 Process the logs that has ThreadId prefix. (These prefixes are added by RIDETraceListener
                    if (resultOutputList[i].Contains("#ThreadId="))
                    {
                        // 1.1.1. Get the ThreadId from the log.
                        //#ThreadId=5|[10/24/2017 5:41:22 PM] CustomerSite.ThermostatsPage_Schedules.SelectThermostat	Info	-----	Found thermostat with name 'Living Room Thermostat'
                        //2017-10-30 12:45:08,449 [9] DEBUG NRobot.Server.Imp.Services.XmlRpcService - [KeywordResult Status=Pass, Output=NRobot.Server.exe Information: 0 : #ThreadId=9|User Authenticated Successfully for customer username = iqgen2prod09
                        //NRobot.Server.exe Information: 0 : #ThreadId=9|Successfully sent command to Disarm partition id = 1
                        string threadId = resultOutputList[i].Substring(
                            resultOutputList[i].IndexOf("#ThreadId") + 10, 
                            resultOutputList[i].IndexOf('|') - resultOutputList[i].IndexOf('=') - 1
                            );
                        
                        // 1.1.2. Check if the log belong to current Thread.
                        if (threadId == Thread.CurrentThread.ManagedThreadId.ToString())
                        {
                            // if yes, remove the ThreadId tag. 
                            // #ThreadId=10|
                            resultOutputList[i] = resultOutputList[i].Substring(
                                0,
                                resultOutputList[i].IndexOf("#ThreadId=")
                                )
                                + resultOutputList[i].Substring(
                                resultOutputList[i].IndexOf('|') + 1,
                                resultOutputList[i].Length - resultOutputList[i].IndexOf('|') - 1
                                );
                        }
                        else
                        {
                            // if not, remove this log entry from the xml response so RIDE use won't see it.
                            resultOutputList.RemoveAt(i);
                            // Since we removed 1 element, do not increase i
                            continue;
                        }
                    }

                    i++;
                }

                // 1.3 Assemble the string and assign back to KeywordOutput.
                result.KeywordOutput = string.Join("\n", resultOutputList.ToArray()) + "\n";

                // 2.0 Try to log keyword execution in database
                if(NRobotService.log == true)
                {
                    ExecutionLogger.LogKeywordExecution(keyword, typename, args.ToList(), get_keyword_arguments(keyword).ToList(), result);
                }
                
				kr = XmlRpcResultBuilder.ToXmlRpcResult(result);
			}
			catch (Exception e)
			{
				Log.Error(String.Format("Exception in method - run_keyword : {0}",e));
				throw new XmlRpcFaultException(1,e.Message);
			}
			return kr;
		}

        /// <summary>
        ﻿  ﻿  /// Get list of arguments for specified Robot Framework keyword.
        ﻿  ﻿  /// </summary>
        public string[] get_keyword_arguments(string friendlyname)
	﻿  ﻿  {
            Log.Debug(String.Format("XmlRpc Method call - get_keyword_arguments {0}", friendlyname));
			try
			{
			    var typename = _threadkeywordtype[Thread.CurrentThread.ManagedThreadId];
                var keyword = _keywordManager.GetKeyword(typename, friendlyname);
			    return keyword.ArgumentNames;
			}
			catch (Exception e)
			{
				Log.Error(String.Format("Exception in method - get_keyword_arguments : {0}",e.Message));
				throw new XmlRpcFaultException(1,e.Message);
			}
	﻿  ﻿  }

        /// <summary>
        /// Get documentation for specified Robot Framework keyword.
        /// Done by reading the .NET compiler generated XML documentation
        /// for the loaded class library.
        /// </summary>
        /// <returns>A documentation string for the given keyword.</returns>
        public string get_keyword_documentation(string friendlyname)
        {
            Log.Debug(String.Format("XmlRpc Method call - get_keyword_documentation {0}", friendlyname));
            try
            {
                //check for INTRO 
                if (String.Equals(friendlyname, CIntro, StringComparison.CurrentCultureIgnoreCase))
                {
                    return String.Empty;
                }
                //check for init
                if (String.Equals(friendlyname, CInit, StringComparison.CurrentCultureIgnoreCase))
                {
                    return String.Empty;
                }
                //get keyword documentation
                var typename = _threadkeywordtype[Thread.CurrentThread.ManagedThreadId];
                var keyword = _keywordManager.GetKeyword(typename, friendlyname);
                var doc = keyword.KeywordDocumentation;
                Log.Debug(String.Format("Keyword documentation, {0}", doc));
                return doc;
            }
            catch (Exception e)
            {
                Log.Error(String.Format("Exception in method - get_keyword_documentation : {0}", e.Message));
                throw new XmlRpcFaultException(1, e.Message);
            }

        }
#endregion

    }
}