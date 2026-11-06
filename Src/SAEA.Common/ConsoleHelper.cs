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
*文件名： ConsoleHelper
*版本号： v26.11.5.1
*唯一标识：537f975a-1f5d-49d6-b1cc-79c7d3aba99f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/08/24 16:31:11
*描述：ConsoleHelper帮助类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 14:31:45
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 Flush()，退出前等待异步队列写完，避免短生命周期进程丢输出
*
*****************************************************************************/
using SAEA.Common.Threading;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;


namespace SAEA.Common
{
    /// <summary>
    /// 控制台帮助类
    /// </summary>
    public static class ConsoleHelper
    {
        /// <summary>
        /// 阻塞集合，用于存储控制台信息
        /// </summary>
        static BlockingCollection<ConsoleInfo> _queue = new BlockingCollection<ConsoleInfo>();

        /// <summary>
        /// 日志文件路径
        /// </summary>
        static string _logFilePath;

        /// <summary>
        /// 日志文件写入流
        /// </summary>
        static StreamWriter _logWriter;

        /// <summary>
        /// 日志写入队列
        /// </summary>
        static BlockingCollection<string> _logQueue = new BlockingCollection<string>();

        /// <summary>
        /// 尚未写出到控制台的行数，供 Flush 判断是否写完
        /// </summary>
        static int _consolePending;

        /// <summary>
        /// 尚未写入日志文件的行数
        /// </summary>
        static int _logPending;

        static ConsoleHelper()
        {
            _ = DateTimeHelper.ToString();

            // 初始化日志文件
            InitLogFile();

            TaskHelper.LongRunning(() =>
            {
                while (true)
                {
                    try
                    {
                        if (_queue.TryTake(out ConsoleInfo consoleInfo, 100))
                        {
                            try
                            {
                                var oldColor = Console.ForegroundColor;
                                Console.ForegroundColor = consoleInfo.Color;
                                var writeStr = DateTimeHelper.ToString() + "  " + consoleInfo.Text;
                                if (consoleInfo.Args != null && consoleInfo.Args.Length > 0)
                                    Console.WriteLine(writeStr, consoleInfo.Args);
                                else
                                    Console.WriteLine(writeStr);
                                Console.ForegroundColor = oldColor;
                            }
                            finally
                            {
                                // 无论写出成功与否都要归零，否则 Flush 会一直等到超时
                                Interlocked.Decrement(ref _consolePending);
                            }
                        }
                        else
                        {
                            continue;
                        }
                    }
                    catch
                    {
                        Thread.Sleep(100);
                    }
                }
            });

            // 启动日志写入线程
            TaskHelper.LongRunning(() =>
            {
                while (true)
                {
                    try
                    {
                        if (_logQueue.TryTake(out string logLine, 100))
                        {
                            try
                            {
                                _logWriter?.WriteLine(logLine);
                                _logWriter?.Flush();
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _logPending);
                            }
                        }
                    }
                    catch
                    {
                        Thread.Sleep(100);
                    }
                }
            });
        }

        /// <summary>
        /// 初始化日志文件
        /// </summary>
        static void InitLogFile()
        {
            try
            {
                var tempPath = Path.GetTempPath();
                var logDir = Path.Combine(tempPath, "SAEA.Sockets.Logs", "Console");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                var fileName = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".log";
                _logFilePath = Path.Combine(logDir, fileName);
                _logWriter = new StreamWriter(_logFilePath, true);
                _logWriter.AutoFlush = true;
            }
            catch
            {
                // 如果创建日志文件失败，忽略错误
            }
        }

        /// <summary>
        /// 获取或设置控制台标题
        /// </summary>
        public static string Title
        {
            get => Console.Title;
            set => Console.Title = value;
        }

        /// <summary>
        /// 输出一行文本到控制台
        /// </summary>
        /// <param name="str">要输出的文本</param>
        public static void WriteLine(string str)
        {
            Interlocked.Increment(ref _consolePending);
            _queue.TryAdd(new ConsoleInfo { Text = str });
            // 同时写入日志文件
            Interlocked.Increment(ref _logPending);
            _logQueue.TryAdd(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + str);
        }

        /// <summary>
        /// 输出一行到控制台
        /// </summary>
        public static void WriteLine()
        {
            Interlocked.Increment(ref _consolePending);
            _queue.TryAdd(new ConsoleInfo { Text = Environment.NewLine });
            // 同时写入日志文件
            Interlocked.Increment(ref _logPending);
            _logQueue.TryAdd(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + Environment.NewLine);
        }

        /// <summary>
        /// 输出一行带参数的文本到控制台
        /// </summary>
        /// <param name="str">要输出的文本</param>
        /// <param name="args">参数</param>
        public static void WriteLine(string str, params object[] args)
        {
            Interlocked.Increment(ref _consolePending);
            _queue.TryAdd(new ConsoleInfo { Text = str, Args = args });
            // 同时写入日志文件
            Interlocked.Increment(ref _logPending);
            try
            {
                var formatted = string.Format(str, args);
                _logQueue.TryAdd(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + formatted);
            }
            catch
            {
                _logQueue.TryAdd(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + str);
            }
        }

        /// <summary>
        /// 输出一行带颜色和参数的文本到控制台
        /// </summary>
        /// <param name="str">要输出的文本</param>
        /// <param name="color">文本颜色</param>
        /// <param name="args">参数</param>
        public static void WriteLine(string str, ConsoleColor color, params object[] args)
        {
            Interlocked.Increment(ref _consolePending);
            _queue.TryAdd(new ConsoleInfo { Text = str, Color = color, Args = args });
            // 同时写入日志文件
            Interlocked.Increment(ref _logPending);
            try
            {
                var formatted = string.Format(str, args);
                _logQueue.TryAdd(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + formatted);
            }
            catch
            {
                _logQueue.TryAdd(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + str);
            }
        }

        /// <summary>
        /// 等待已入队的控制台与日志输出全部写完
        /// </summary>
        /// <remarks>
        /// 输出由后台线程异步完成，短生命周期进程（命令行工具、一次性任务）在退出前必须调用本方法，
        /// 否则队列里未写出的行会随进程结束丢失。最多等待 2 秒，超时后直接返回。
        /// </remarks>
        public static void Flush()
        {
            WaitPending(ref _consolePending);
            WaitPending(ref _logPending);
            _logWriter?.Flush();
        }

        /// <summary>
        /// 等待指定待写计数归零
        /// </summary>
        /// <param name="pending">待写计数</param>
        static void WaitPending(ref int pending)
        {
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (Volatile.Read(ref pending) > 0 && DateTime.UtcNow < deadline)
                Thread.Sleep(5);
        }

        /// <summary>
        /// 从控制台读取一行文本
        /// </summary>
        /// <returns>读取的文本</returns>
        public static string ReadLine()
        {
            try
            {
                return Console.ReadLine();
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <summary>
    /// 控制台信息类
    /// </summary>
    public class ConsoleInfo
    {
        /// <summary>
        /// 文本内容
        /// </summary>
        public string Text { get; set; }
        /// <summary>
        /// 参数
        /// </summary>
        public object[] Args { get; set; } = null;
        /// <summary>
        /// 文本颜色
        /// </summary>
        public ConsoleColor Color { get; set; } = Console.ForegroundColor;
    }
}