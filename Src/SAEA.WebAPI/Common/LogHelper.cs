using SAEA.Commom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SAEA.WebAPI.Common
{
    public static class LogHelper
    {
        static string logPath = string.Empty;


        private static void Write(string type, string msg)
        {
            if (string.IsNullOrEmpty(logPath))
                logPath = PathHelper.GetCurrentPath("Logs");
            var fileName = PathHelper.GetFilePath(logPath, type + DateTimeHelper.ToString("yyyyMMdd") + ".log");
            File.AppendAllTextAsync(fileName, DateTimeHelper.ToString() + "  " + msg + Environment.NewLine);
        }


        public static void WriteError(string des, Exception ex)
        {
            Write("Error", des + " " + ex.Message);
        }

        public static void Write(string des)
        {
            Write("Info", des);
        }
    }
}
