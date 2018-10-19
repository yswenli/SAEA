/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.NatSocket
*文件名： Class1
*版本号： V2.2.1.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： V2.2.1.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace SAEA.NatSocket.Utils
{
    internal static class StreamExtensions
    {
        internal static string ReadAsMany(this System.IO.StreamReader stream, int bytesToRead)
        {
            var buffer = new char[bytesToRead];
            stream.ReadBlock(buffer, 0, bytesToRead);
            return new string(buffer);
        }

        internal static string GetXmlElementText(this XmlNode node, string elementName)
        {
            XmlElement element = node[elementName];
            return element != null ? element.InnerText : string.Empty;
        }

        internal static bool ContainsIgnoreCase(this string s, string pattern)
        {
            return s.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static void LogInfo(this TraceSource source, string format, params object[] args)
        {
            try
            {
                source.TraceEvent(TraceEventType.Information, 0, format, args);

                ConsoleHelper.WriteLine(format, args);
            }
            catch (ObjectDisposedException)
            {
                source.Switch.Level = SourceLevels.Off;
            }
        }

        internal static void LogWarn(this TraceSource source, string format, params object[] args)
        {
            try
            {
                source.TraceEvent(TraceEventType.Warning, 0, format, args);
            }
            catch (ObjectDisposedException)
            {
                source.Switch.Level = SourceLevels.Off;
            }
        }


        internal static void LogError(this TraceSource source, string format, params object[] args)
        {
            try
            {
                source.TraceEvent(TraceEventType.Error, 0, format, args);
            }
            catch (ObjectDisposedException)
            {
                source.Switch.Level = SourceLevels.Off;
            }
        }

        internal static string ToPrintableXml(this XmlDocument document)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new XmlTextWriter(stream, Encoding.Unicode))
                {
                    try
                    {
                        writer.Formatting = Formatting.Indented;

                        document.WriteContentTo(writer);
                        writer.Flush();
                        stream.Flush();

                        // Have to rewind the MemoryStream in order to read
                        // its contents.
                        stream.Position = 0;

                        // Read MemoryStream contents into a StreamReader.
                        var reader = new System.IO.StreamReader(stream);

                        // Extract the text from the StreamReader.
                        return reader.ReadToEnd();
                    }
                    catch (Exception)
                    {
                        return document.ToString();
                    }
                }
            }
        }


        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout)
        {

            var timeoutCancellationTokenSource = new CancellationTokenSource();

            Task completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
            if (completedTask == task)
            {
                timeoutCancellationTokenSource.Cancel();
                return await task;
            }
            throw new TimeoutException(
                "The operation has timed out. The network is broken, router has gone or is too busy.");
        }
    }
}
