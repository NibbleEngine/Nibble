using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace NbCore
{
    public enum LogVerbosityLevel
    {
        HIDEBUG,
        DEBUG,
        INFO,
        WARNING,
        ERROR
    }

    public struct LogElement
    {
        public string message;
        public string sender;
        public string type;

        public LogElement(string s, string msg, string typ)
        {
            message = msg;
            sender = s;
            type = typ;
        }
    }

    public delegate void LogEventHandler(LogElement msg);

    public class NbLogger
    {
        public event LogEventHandler LogEvent;
        public LogVerbosityLevel LogVerbosity;
        public StreamWriter loggingSr;

        public NbLogger()
        {
            //Setup Logger
            loggingSr = new StreamWriter("log.out");
        }

        public virtual void Log(object sender, string msg, LogVerbosityLevel lvl) {
            //Print Shit
            if (lvl >= LogVerbosity)
            {
                Console.WriteLine($"{sender.ToString().ToUpper()} - {lvl.ToString().ToUpper()} - {msg}");

                LogElement elem = new()
                {
                    message = msg,
                    sender = sender.ToString().ToUpper(),
                    type = lvl.ToString()
                };

                loggingSr.WriteLine(msg);
                loggingSr.Flush();
                LogEvent?.Invoke(elem);
            }
        }
    }

}
