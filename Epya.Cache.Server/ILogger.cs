using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Epya.Cache.Server
{
    public interface ILogger
    {
        LogLevel TargetLogLevel { set; get; }
        string Guid { get; set; }    
        string Duration { get; }
        List<logData> Logs { set; get; }
        void Log(logData log);
        void LogDirectly(logData log);
        void WriteAll();

    }

    public enum LogLevel
    {
        All = 0,
        Inf = 2,
        Err = 4,
        Deb = 8,
        Kpi=16,
        time=32
    }

    public class logData
    {
        public string Message { set; get; }
        public LogLevel Level { set; get; }
        public DateTime DateTime { set; get; }
    }
}
