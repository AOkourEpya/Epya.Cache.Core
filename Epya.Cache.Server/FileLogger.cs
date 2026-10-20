using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Epya.Cache.Server
{
    public class FileLogger : ILogger
    {
        private readonly string filePath;
        private static readonly object fileLock = new object();

        public LogLevel TargetLogLevel { get; set; }

        public string Guid { get; set; }
        public string Duration
        {
            get
            {
                if (this.Logs == null || this.Logs.Count == 0)
                    return "";
                return this.Logs.Last().DateTime.Subtract(this.Logs.First().DateTime).ToString("c");
            }
        }
        public List<logData> Logs { get; set; } = new List<logData>();


        public FileLogger(string filePath, LogLevel targetLogLevel = LogLevel.All)
        {

            Guid = System.Guid.NewGuid().ToString();
            TargetLogLevel = targetLogLevel;
            this.filePath = filePath;
            Logs.Add(new logData() { DateTime = DateTime.Now, Level = LogLevel.Inf, Message = "#############################################################################################" });
            Logs.Add(new logData() { DateTime = DateTime.Now, Level = LogLevel.Inf, Message = Guid });

        }

        public void Log(logData log)
        {
            if (TargetLogLevel.HasFlag(log.Level) || TargetLogLevel == LogLevel.All)
                Logs.Add(log);
        }

        public void LogDirectly(logData log)
        {
            if (TargetLogLevel.HasFlag(log.Level) || TargetLogLevel == LogLevel.All)
                WriteLog(log);
        }

        private void WriteLog(logData log)
        {
            Task.Run(() =>
            {
                string logEntry = $"[{log.DateTime:yyyy-MM-dd HH:mm:ss}] [{log.Level}] {log.Message}";
                lock (fileLock) // Thread-safe file writing
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    if (!string.IsNullOrEmpty(log.Message))
                        File.AppendAllText(filePath, logEntry + Environment.NewLine);
                    else
                        File.AppendAllText(filePath, Environment.NewLine);

                }
            });

          
        }

        public void WriteAll()
        {
          
            Logs.Insert(2, new logData() { DateTime = DateTime.Now, Level = LogLevel.Inf, Message = Duration });
            Logs.Add(new logData() { DateTime = DateTime.Now, Level = LogLevel.Inf, Message = "#############################################################################################" });
            Logs.Add(new logData() { DateTime = DateTime.Now, Level = LogLevel.Inf, Message = "" });

            if (Logs != null && Logs.Count > 0)
            {
                foreach (logData log in Logs) { WriteLog(log); }
            }


          
            Logs = new List<logData>();

        }

      
    }


}
