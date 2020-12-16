/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Common
*文件名： LogHelper
*版本号： v5.0.0.1
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
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common.IO;
using SAEA.Common.Serialization;
using SAEA.Common.Threading;
using System;
using System.Collections.Concurrent;
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
        static string _logPath;

        static ConcurrentBag<LogItem> _cache;

        /// <summary>
        /// 日志
        /// </summary>
        static LogHelper()
        {
            _cache = new ConcurrentBag<LogItem>();

            _logPath = PathHelper.GetCurrentPath("Logs");

            TaskHelper.LongRunning(() =>
            {
                while (true)
                {
                    if (_cache.IsEmpty)
                    {
                        ThreadHelper.Sleep(500);
                    }
                    else
                    {
                        var count = _cache.Count;

                        for (int i = 0; i < count; i++)
                        {
                            if (_cache.TryTake(out LogItem logItem))
                            {
                                try
                                {
                                    var fileName = PathHelper.GetFilePath(_logPath, logItem.Type + DateTimeHelper.ToString("yyyyMMdd") + ".log");
                                    File.AppendAllText(fileName, $"{DateTimeHelper.ToString()}   {logItem.Type}   {logItem.Msg}{Environment.NewLine}", Encoding.UTF8);
                                }
                                catch { }
                            }
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Write
        /// </summary>
        /// <param name="type"></param>
        /// <param name="msg"></param>
        static void Write(string type, string msg)
        {
            _cache.Add(new LogItem() { Type = type, Msg = msg });
        }

        /// <summary>
        /// Error
        /// </summary>
        /// <param name="des"></param>
        /// <param name="ex"></param>
        /// <param name="params"></param>
        public static void Error(string des, Exception ex, params object[] @params)
        {
            string paramStr = string.Empty;

            if (@params != null && @params.Any())
            {
                paramStr = SerializeHelper.Serialize(@params);
            }
            Write($"[Error]", $"{des}\terr:{SerializeHelper.Serialize(ex)},params:{paramStr}");
        }

        /// <summary>
        /// Warn
        /// </summary>
        /// <param name="des"></param>
        /// <param name="ex"></param>
        /// <param name="params"></param>
        public static void Warn(string des, Exception ex, params object[] @params)
        {
            string paramStr = string.Empty;

            if (@params != null && @params.Any())
            {
                paramStr = SerializeHelper.Serialize(@params);
            }
            Write($"[Warn]", $"{des}\terr:{SerializeHelper.Serialize(ex)},params:{paramStr}");
        }

        /// <summary>
        /// Info
        /// </summary>
        /// <param name="des"></param>
        /// <param name="params"></param>
        public static void Info(string des, params object[] @params)
        {
            string paramStr = string.Empty;

            if (@params != null && @params.Any())
            {
                paramStr = SerializeHelper.Serialize(@params);
            }
            Write($"[Info]", $"{des}\tparams:{paramStr}");
        }

        /// <summary>
        /// Debug
        /// </summary>
        /// <param name="des"></param>
        /// <param name="params"></param>
        public static void Debug(string des, params object[] @params)
        {
            string paramStr = string.Empty;

            if (@params != null && @params.Any())
            {
                paramStr = SerializeHelper.Serialize(@params);
            }
            ConsoleHelper.WriteLine($"[Debug] {des}\tparams:{paramStr}");
            Write($"[Debug]", $"{des}\tparams:{paramStr}");
        }

        /// <summary>
        /// Debug
        /// </summary>
        /// <param name="des"></param>
        /// <param name="data"></param>
        public static void Debug(string des, byte[] data)
        {
            var result = "内容为空";
            if (data != null && data.Any())
            {
                result = Encoding.UTF8.GetString(data);
            }
            ConsoleHelper.WriteLine($"[Debug] {des}\t{result}");
            Write("[Debug]", $"{des}\t{result}");
        }
    }

    /// <summary>
    /// LogItem
    /// </summary>
    internal struct LogItem
    {
        public string Type { get; set; }

        public string Msg { get; set; }
    }
}
