using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Epya.Cache.Server
{
    public class ConsoleLogger : ILogger
    {
        public LogLevel TargetLogLevel { get; set; }

        public string Guid { get; set; }
        public string Duration { 
            get {
                if (this.Logs == null || this.Logs.Count == 0)
                    return "";
                return this.Logs.Last().DateTime.Subtract(this.Logs.First().DateTime).ToString("c");
            }
        }
        public List<logData> Logs { get; set; } = new List<logData>();


        public ConsoleLogger(LogLevel targetLogLevel= LogLevel.All) {
            TargetLogLevel = targetLogLevel;
            init();
        }  

        private void init()
        {

            Guid = System.Guid.NewGuid().ToString();
            Logs = new List<logData>();
            Logs.Add(new logData() { DateTime = DateTime.Now, Level = LogLevel.Inf, Message = "#######################################################################" });
            Logs.Add(new logData() { DateTime = DateTime.Now, Level = LogLevel.Inf, Message = Guid });
        }

        public void Log(logData log)
        {
            if (TargetLogLevel.HasFlag(log.Level) || TargetLogLevel== LogLevel.All)
                Logs.Add(log);

           
        }

        public void LogDirectly(logData log)
        {
            if (TargetLogLevel.HasFlag(log.Level) || TargetLogLevel == LogLevel.All)
                WriteLog(log);
        }

        private void WriteLog(logData log)
        {
            Console.ForegroundColor = (log.Level == LogLevel.Err) ? ConsoleColor.Red : ConsoleColor.White;
            Console.WriteLine($"[{log.DateTime:yyyy-MM-dd HH:mm:ss}] [{log.Level}] {log.Message}");
            Console.ResetColor();
        }

        public void WriteAll()
        {

            Logs.Insert(2, new logData() { DateTime = DateTime.Now, Level = LogLevel.Inf, Message = Duration });
            Logs.Add(new logData() { DateTime = DateTime.Now, Level = LogLevel.Inf, Message = "#######################################################################" });
            Logs.Add(new logData() { DateTime = DateTime.Now, Level = LogLevel.Inf, Message = "" });


            if (Logs != null && Logs.Count > 0)
            {
                foreach (logData log in Logs) { WriteLog(log); }
            }

            init();

        }

      
    }

}
