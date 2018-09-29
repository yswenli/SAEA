using SAEA.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SAEA.WebAPI.Common
{
    /// <summary>
    /// 生成日志
    /// </summary>
    public static class LogHelper
    {
        static string logPath = string.Empty;
        
        private static void Write(string type, string msg)
        {
            try
            {
                if (string.IsNullOrEmpty(logPath))
                    logPath = PathHelper.GetCurrentPath("Logs");
                var fileName = PathHelper.GetFilePath(logPath, type + DateTimeHelper.ToString("yyyyMMdd") + ".log");
                File.AppendAllText(fileName, DateTimeHelper.ToString() + "  " + msg + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
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
