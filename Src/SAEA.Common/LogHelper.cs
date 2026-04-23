/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Common
*文件名： LogHelper
*版本号： v26.4.23.1
*唯一标识：a4bb0af5-73b7-4ba9-ad53-85e3f16ad4a4
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/03/12 17:05:14
*描述：LogHelper帮助类
*
*=====================================================================
*修改标记
*修改时间：2019/03/12 17:05:14
*修改人： yswenli
*版本号： v26.4.23.1
*描述：LogHelper帮助类
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

        static long _maxFileSize = 10 * 1024 * 1024;
        static int _maxLogFileCount = 10;
        static bool _fileSizeLimitEnabled = false;

        public static void SetMaxFileSize(long maxSize)
        {
            _maxFileSize = maxSize;
            _fileSizeLimitEnabled = true;
        }

        public static void SetMaxLogFileCount(int maxCount)
        {
            _maxLogFileCount = maxCount;
        }

        public static void SetLogPath(string path)
        {
            _logPath = path;
        }

        static void WriteToFile(string fileName, string msg)
        {
            if (_fileSizeLimitEnabled)
            {
                CheckAndRotateFile(fileName);
            }
            File.AppendAllText(fileName, msg, Encoding.UTF8);
        }

        static void CheckAndRotateFile(string fileName)
        {
            try
            {
                if (File.Exists(fileName))
                {
                    var fileInfo = new FileInfo(fileName);
                    if (fileInfo.Length >= _maxFileSize)
                    {
                        RotateFiles(fileName);
                    }
                }
            }
            catch { }
        }

        static void RotateFiles(string fileName)
        {
            try
            {
                var baseName = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                var dir = Path.GetDirectoryName(fileName);

                var timestamp = DateTimeHelper.ToString("yyyyMMdd_HHmmss");
                var archiveName = Path.Combine(dir, $"{baseName}_{timestamp}{ext}");
                File.Move(fileName, archiveName);

                CleanupOldFiles(dir, baseName, ext);
            }
            catch { }
        }

        static void CleanupOldFiles(string dir, string baseName, string ext)
        {
            try
            {
                var files = Directory.GetFiles(dir, $"{baseName}_*{ext}")
                    .OrderByDescending(f => f)
                    .Skip(_maxLogFileCount);

                foreach (var file in files)
                {
                    try { File.Delete(file); } catch { }
                }
            }
            catch { }
        }

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
                                    var msg = $"{DateTimeHelper.ToString()}   {logItem.Type}   {logItem.Msg}{Environment.NewLine}";
                                    Console.WriteLine(msg);
                                    WriteToFile(fileName, msg);
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