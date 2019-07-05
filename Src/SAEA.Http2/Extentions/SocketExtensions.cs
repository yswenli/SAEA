/****************************************************************************
*项目名称：SAEA.Http2.Extentions
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Extentions
*类 名 称：SocketExtensions
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/28 13:38:13
*描述：
*=====================================================================
*修改时间：2019/6/28 13:38:13
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Interfaces;
using SAEA.Http2.Model;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SAEA.Http2.Extentions
{
    /// <summary>
    /// system.net.sockets的扩展方法
    /// </summary>
    public static class SocketExtensions
    {
        public struct CreateStreamsResult
        {
            public IReadableByteStream ReadableStream;

            public IWriteAndCloseableByteStream WriteableStream;
        }

        /// <summary>
        /// 在.NET套接字上创建所需的流抽象。
        /// </summary>
        /// <param name="socket"></param>
        /// <returns></returns>
        public static CreateStreamsResult CreateStreams(this Socket socket)
        {
            if (socket == null) throw new ArgumentNullException(nameof(socket));
            var wrappedStream = new SocketWrapper(socket);
            return new CreateStreamsResult
            {
                ReadableStream = wrappedStream,
                WriteableStream = wrappedStream,
            };
        }

        internal class SocketWrapper : IReadableByteStream, IWriteAndCloseableByteStream
        {
            private Socket socket;

            private bool tryNonBlockingRead = false;

            public SocketWrapper(Socket socket)
            {
                this.socket = socket;

                //socket.Blocking = false;
            }

            public ValueTask<StreamReadResult> ReadAsync(ArraySegment<byte> buffer)
            {
                if (buffer.Count == 0)
                {
                    throw new Exception("不支持读取0字节");
                }

                var offset = buffer.Offset;
                var count = buffer.Count;

                if (tryNonBlockingRead)
                {

                    SocketError ec;
                    var rcvd = socket.Receive(buffer.Array, offset, count, SocketFlags.None, out ec);
                    if (ec != SocketError.Success &&
                        ec != SocketError.WouldBlock &&
                        ec != SocketError.TryAgain)
                    {
                        return new ValueTask<StreamReadResult>(
                            Task.FromException<StreamReadResult>(
                                new SocketException((int)ec)));
                    }

                    if (rcvd != count)
                    {
                        tryNonBlockingRead = false;
                    }

                    if (ec == SocketError.Success)
                    {
                        return new ValueTask<StreamReadResult>(
                            new StreamReadResult
                            {
                                BytesRead = rcvd,
                                EndOfStream = rcvd == 0,
                            });
                    }

                    if (rcvd != 0)
                    {
                        throw new Exception("在Tryagain案例中意外接收数据");
                    }
                }

                var readTask = socket.ReceiveAsync(buffer, SocketFlags.None);

                Task<StreamReadResult> transformedTask = readTask.ContinueWith(tt =>
                {
                    if (tt.Exception != null)
                    {
                        throw tt.Exception;
                    }

                    var res = tt.Result;
                    if (res == count)
                    {
                        tryNonBlockingRead = true;
                    }

                    return new StreamReadResult
                    {
                        BytesRead = res,
                        EndOfStream = res == 0,
                    };
                });

                return new ValueTask<StreamReadResult>(transformedTask);
            }

            public Task WriteAsync(ArraySegment<byte> buffer)
            {
                if (buffer.Count == 0)
                {
                    return Task.CompletedTask;
                }

                SocketError ec;
                var sent = socket.Send(
                    buffer.Array, buffer.Offset, buffer.Count, SocketFlags.None, out ec);

                if (ec != SocketError.Success &&
                    ec != SocketError.WouldBlock &&
                    ec != SocketError.TryAgain)
                {
                    throw new SocketException((int)ec);
                }

                if (sent == buffer.Count)
                {
                    return Task.CompletedTask;
                }


                var remaining = new ArraySegment<byte>(
                    buffer.Array, buffer.Offset + sent, buffer.Count - sent);

                return socket.SendAsync(remaining, SocketFlags.None);
            }

            public Task CloseAsync()
            {
                socket.Dispose();
                return Task.CompletedTask;
            }
        }
    }
}
