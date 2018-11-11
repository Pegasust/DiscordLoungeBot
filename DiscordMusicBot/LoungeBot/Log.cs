using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordMusicBot.LoungeBot
{
    enum LogType
    {
        Info,
        Warning,
        Error,
        Debug,
        Timetracing,
        Success,
    }
    enum EditMethod
    {
        AddPrefix,
        AddSuffix,
        Replace
    }
    internal static class LogHelper
    {
        private static string tempMsg;
        private static LogType tempLogType;

        public static void LogTemp(string msg, LogType logType = LogType.Info)
        {
            tempMsg = msg;
            tempLogType = logType;
        }
        public static void ChangeTempMsg(string str, EditMethod method)
        {
            switch (method)
            {
                case EditMethod.AddPrefix:
                    tempMsg = str + tempMsg;
                    break;
                case EditMethod.AddSuffix:
                    tempMsg += str;
                    break;
                case EditMethod.Replace:
                    tempMsg = str;
                    break;
            }
        }
        public static void ChangeTempMsg(string str, EditMethod method, LogType newLogType)
        {
            ChangeTempMsg(str, method);
            tempLogType = newLogType;
        }
        public static void SendTempMsg()
        {
            Logln(tempMsg, tempLogType);
            tempMsg = null;
        }
        public static void Log(string msg, LogType logType = LogType.Info)
        {
            Print(msg, ColorOf(logType));
        }
        public static void Logln(string msg, LogType logType = LogType.Info)
        {
            Println(msg, ColorOf(logType));
        }
        private static ConsoleColor ColorOf(LogType logType)
        {
            switch (logType)
            {
                case LogType.Info:
                    return ConsoleColor.Gray;
                case LogType.Warning:
                    return ConsoleColor.Cyan;
                case LogType.Error:
                    return ConsoleColor.Red;
                case LogType.Success:
                    return ConsoleColor.Green;
#if DEBUG
                case LogType.Debug:
                    return ConsoleColor.Magenta;
#endif
#if TRACETIME
                case LogType.Timetracing:
                    return ConsoleColor.Yellow;
#endif
                default:
                    return ConsoleColor.White;
            }
        }
        private static void Print(string msg, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write(msg);
            Console.ResetColor();
        }
        private static void Println(string msg, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(msg);
            Console.ResetColor();
        }

    }
}
