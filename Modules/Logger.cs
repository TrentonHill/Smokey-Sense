using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.COM.Surogate.Modules
{
    internal static class Logger
    {
        public static LogLevel MinimumLogLevel { get; set; } = LogLevel.Debug;

        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error
        }

        public static void Log(string message)
        {
            Console.WriteLine($"[LOG - {DateTime.Now.ToString("HH:mm:ss")}] {message}");
        }

        public static void Log(string message, LogLevel level)
        {
            if (level < MinimumLogLevel)
                return;

            string prefix = LogLevelToString(level);
            Console.WriteLine($"{prefix} [{DateTime.Now.ToString("HH:mm:ss")}] {message}");
        }

        public static void LogInfo(string message)
        {
            Log(message, LogLevel.Info);
        }
        public static void LogWarning(string message)
        {
            Log(message, LogLevel.Warning);
        }
        public static void LogError(string message)
        {
            Log(message, LogLevel.Error);
        }
        public static void LogDebug(string message)
        {
            Log(message, LogLevel.Debug);
        }

        static string LogLevelToString (LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return "[D]:";
                case LogLevel.Info:
                    return "[I]:";
                case LogLevel.Warning:
                    return "[W]:";
                case LogLevel.Error:
                    return "[E]:";
                default:
                    return "[LOG]:";
            }
        }
    }
}
