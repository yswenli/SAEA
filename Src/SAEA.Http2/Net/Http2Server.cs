/****************************************************************************
*项目名称：SAEA.Http2.Net
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Net
*类 名 称：SocketConnect
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/28 11:07:32
*描述：
*=====================================================================
*修改时间：2019/6/28 11:07:32
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.Http2.Core;
using SAEA.Http2.Extentions;
using SAEA.Http2.Interfaces;
using SAEA.Http2.Model;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SAEA.Http2.Net
{
    /// <summary>
    /// http2服务端
    /// </summary>
    public class Http2Server
    {
        IServerSokcet _serverSocket;

        IHttp2Invoker _invoker;

        /// <summary>
        /// http2服务端
        /// </summary>
        /// <param name="invoker"></param>
        /// <param name="port"></param>
        /// <param name="count"></param>
        /// <param name="size"></param>
        /// <param name="timeOut"></param>
        public Http2Server(IHttp2Invoker invoker = null, int port = 39654, int count = 100, int size = 1024, int timeOut = 180 * 1000)
        {
            var options = SocketOptionBuilder.Instance.SetSocket(Sockets.Model.SAEASocketType.Tcp)
                .UseStream()
                .SetPort(port)
                .SetCount(count)
                .SetReadBufferSize(size)
                .SetWriteBufferSize(size)
                .SetTimeOut(timeOut)
                .Build();

            _invoker = invoker;

            _serverSocket = SocketFactory.CreateServerSocket(options);
            _serverSocket.OnAccepted += _serverSocket_OnAccepted;
        }

        private void _serverSocket_OnAccepted(object obj)
        {
            var ci = (ChannelInfo)obj;

            if (ci != null)
            {
                var config = new ConnectionConfigurationBuilder(isServer: true)
                            .UseStreamListener(Http2Handler)
                            .UseHuffmanStrategy(HuffmanStrategy.Never)
                            .UseBufferPool(Buffers.Pool)
                            .Build();

                var wrappedStreams = ci.ClientSocket.CreateStreams();

                var http2Con = new Connection(config, wrappedStreams.ReadableStream, wrappedStreams.WriteableStream, options: new Connection.Options());
            }
        }


        bool Http2Handler(IStream stream)
        {
            try
            {
                using (Http2Context http2Context = new Http2Context(stream, _invoker))
                {
                    http2Context.Invoke();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("处理请求时出错: {0}", e.Message);
                stream.Cancel();
                return false;
            }
            return true;
        }


        public void Start()
        {
            _serverSocket.Start();
        }


        public void Stop()
        {
            _serverSocket.Stop();
        }

    }
}
