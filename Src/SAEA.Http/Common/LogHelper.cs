/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Http.Base
*文件名： LogHelper
*版本号： V4.1.2.2
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
*版本号： V4.1.2.2
*描述：
*
*****************************************************************************/
using SAEA.Common;
using System;
using System.IO;
using System.Text;

namespace SAEA.Http.Common
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
