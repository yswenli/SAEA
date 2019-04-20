/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Common
*文件名： LogHelper
*版本号： v4.3.3.7
*唯一标识：4eebbaa7-1781-4521-ab57-4bc9c8d43a84
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/8/5 13:31:23
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/8/5 13:31:23
*修改人： yswenli
*版本号： v4.3.3.7
*描述：
*
*****************************************************************************/
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace SAEA.Common
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
                File.AppendAllText(fileName, $"{DateTimeHelper.ToString()}   {type}   {msg}{Environment.NewLine}", Encoding.UTF8);
            }
            catch { }
        }


        public static void Error(string des, Exception ex)
        {
            Write("[Error]", des + " " + ex.Message);
        }

        public static void Warn(string des, Exception ex)
        {
            Write("[Warn]", des + " " + ex.Message);
        }

        public static void Info(string des)
        {
            Write("[Info]", des);
        }

        public static void Debug(params string[] des)
        {
            if (des != null && des.Any())
            {
                StringBuilder sb = new StringBuilder();
                foreach (var item in des)
                {
                    sb.Append(item);
                }
                Write("[Debug]", sb.ToString());
            }

        }

        public static void Debug(byte[] data)
        {
            var result = "内容为空";
            if (data != null && data.Any())
            {
                result = Encoding.UTF8.GetString(data);
            }
            Write("[Debug]", result);
        }
    }
}
