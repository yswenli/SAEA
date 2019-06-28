/****************************************************************************
*项目名称：SAEA.Http2.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Core
*类 名 称：ClientPreface
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:47:19
*描述：
*=====================================================================
*修改时间：2019/6/27 16:47:19
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Extentions;
using SAEA.Http2.Interfaces;
using SAEA.Http2.Model;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Http2.Core
{
    /// <summary>
    /// 用于处理HTTP/2客户端连接前言的工具
    /// </summary>
    public static class ClientPreface
    {
        public const string String = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n";

        public static readonly byte[] Bytes = Encoding.ASCII.GetBytes(ClientPreface.String);


        public static int Length
        {
            get { return Bytes.Length; }
        }

        /// <summary>
        /// 将序言写入给定流
        /// </summary>
        public static Task WriteAsync(IWriteableByteStream stream)
        {
            return stream.WriteAsync(new ArraySegment<byte>(Bytes));
        }

        /// <summary>
        /// 从给定流读取序言并将其与预期值进行比较。
        /// </summary>
        public static async ValueTask<DoneHandle> ReadAsync(IReadableByteStream stream)
        {
            var buffer = new byte[Length];

            await stream.ReadAll(new ArraySegment<byte>(buffer));

            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] != Bytes[i])
                {
                    throw new Exception("接收到无效前缀");
                }
            }

            return DoneHandle.Instance;
        }

        /// <summary>
        /// 从给定流读取序言并将其与预期值进行比较
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="timeoutMillis"></param>
        /// <returns></returns>
        public static async ValueTask<DoneHandle> ReadAsync(
            IReadableByteStream stream, int timeoutMillis)
        {
            if (timeoutMillis < 0) throw new ArgumentException(nameof(timeoutMillis));
            else if (timeoutMillis == 0)
            {
                return await ReadAsync(stream);
            }

            var cts = new CancellationTokenSource();
            var readTask = ReadAsync(stream).AsTask();
            var timerTask = Task.Delay(timeoutMillis, cts.Token);

            var finishedTask = await Task.WhenAny(readTask, timerTask);
            var hasTimeout = ReferenceEquals(timerTask, finishedTask);

            cts.Cancel();
            cts.Dispose();

            if (hasTimeout) throw new TimeoutException();

            return readTask.Result;
        }
    }
}
