using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.Logging.TraceListeners;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace NRobot.Server.Imp.Domain
{
    public class RIDETraceListener : TextWriterTraceListener
    {
        public RIDETraceListener(Stream stream)
            : base(stream)
        {

        }

        public override void WriteLine(string message)
        {
            // add ThreadId in the beginning of the message
            base.WriteLine(string.Format("#ThreadId={0}|{1}", Thread.CurrentThread.ManagedThreadId, message));
        }
    }
}
