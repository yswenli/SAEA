/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Commom
*文件名： ConsoleHelper
*版本号： v7.0.0.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：控制台帮助类
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v7.0.0.1
*描述：控制台帮助类
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Threading;

using SAEA.Common.Threading;


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

        static ConsoleHelper()
        {
            _ = DateTimeHelper.ToString();

            TaskHelper.LongRunning(() =>
            {
                while (true)
                {
                    try
                    {
                        if (_queue.TryTake(out ConsoleInfo consoleInfo, 100))
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
            _queue.TryAdd(new ConsoleInfo { Text = str });
        }

        /// <summary>
        /// 输出一行带参数的文本到控制台
        /// </summary>
        /// <param name="str">要输出的文本</param>
        /// <param name="args">参数</param>
        public static void WriteLine(string str, params object[] args)
        {
            _queue.TryAdd(new ConsoleInfo { Text = str, Args = args });
        }

        /// <summary>
        /// 输出一行带颜色和参数的文本到控制台
        /// </summary>
        /// <param name="str">要输出的文本</param>
        /// <param name="color">文本颜色</param>
        /// <param name="args">参数</param>
        public static void WriteLine(string str, ConsoleColor color, params object[] args)
        {
            _queue.TryAdd(new ConsoleInfo { Text = str, Color = color, Args = args });
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
