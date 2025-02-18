/****************************************************************************
*项目名称：SAEA.WebSocket.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.WebSocket.Core
*类 名 称：WSServerImpl
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/13 15:37:28
*描述：
*=====================================================================
*修改时间：2019/6/13 15:37:28
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;

using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Sockets;
using SAEA.WebSocket.Model;
using SAEA.WebSocket.Type;

namespace SAEA.WebSocket.Core
{
    /// <summary>
    /// websocket server,
    /// iocp实现
    /// </summary>
    internal class WSServerImpl : IWSServer
    {
        // 服务器Socket
        IServerSocket _server;

        // 客户端连接事件
        public event Action<string> OnConnected;

        // 客户端消息事件
        public event Action<string, WSProtocal> OnMessage;

        // 客户端断开连接事件
        public event Action<string> OnDisconnected;

        // 线程锁对象
        object _locker = new object();

        // 客户端列表
        public List<string> Clients { set; get; } = new List<string>();

        // 数据读取批处理器
        internal ClassificationBatcher _reader;

        // 数据写入批处理器
        internal ClassificationBatcher _writer;

        /// <summary>
        /// websocket server
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="count">连接数</param>
        public WSServerImpl(int port = 39654, int bufferSize = 1024, int count = 10000)
        {
            _reader = new ClassificationBatcher(10000, 50);
            _reader.OnBatched += _reader_OnBatched;
            _writer = new ClassificationBatcher(10000, 50);
            _writer.OnBatched += _writer_OnBatched;

            var option = SocketOptionBuilder.Instance
                .SetSocket()
                .UseIocp(new WSContext())
                .SetPort(port)
                .SetReadBufferSize(bufferSize)
                .SetWriteBufferSize(bufferSize)
                .SetMaxConnects(count)
                .Build();

            _server = SocketFactory.CreateServerSocket(option);

            _server.OnReceive += _server_OnReceive;

            _server.OnDisconnected += _server_OnDisconnected;
        }

        /// <summary>
        /// 处理批量读取数据
        /// </summary>
        /// <param name="id">客户端ID</param>
        /// <param name="data">数据</param>
        void _reader_OnBatched(string id, byte[] data)
        {
            var ut = _server.SessionManager.Get(id) as WSUserToken;
            if (ut == null) return;
            var coder = (WSCoder)ut.Coder;
            var msgs = coder.Decode(data);
            if (msgs == null || msgs.Count < 1) return;
            foreach (var msg in msgs)
            {
                var wsProtocal = (WSProtocal)msg;
                switch (wsProtocal.Type)
                {
                    case (byte)WSProtocalType.Close:
                        ReplyClose(ut.ID, wsProtocal);
                        break;
                    case (byte)WSProtocalType.Ping:
                        ReplyPong(ut.ID, wsProtocal);
                        break;
                    case (byte)WSProtocalType.Binary:
                    case (byte)WSProtocalType.Text:
                    case (byte)WSProtocalType.Cont:
                        OnMessage?.Invoke(ut.ID, (WSProtocal)msg);
                        break;
                    case (byte)WSProtocalType.Pong:
                        break;
                    default:
                        var error = string.Format("收到未定义的Opcode={0}", msg.Type);
                        break;
                }
            }
        }

        /// <summary>
        /// 处理批量写入数据
        /// </summary>
        /// <param name="id">客户端ID</param>
        /// <param name="data">数据</param>
        void _writer_OnBatched(string id, byte[] data)
        {
            _server.SendAsync(id, data);
        }

        /// <summary>
        /// 处理客户端断开连接
        /// </summary>
        /// <param name="id">客户端ID</param>
        /// <param name="ex">异常信息</param>
        private void _server_OnDisconnected(string id, Exception ex)
        {
            lock (_locker)
                Clients.Remove(id);
            OnDisconnected?.Invoke(id);
        }

        /// <summary>
        /// 处理接收数据
        /// </summary>
        /// <param name="currentObj">当前对象</param>
        /// <param name="data">数据</param>
        private void _server_OnReceive(object currentObj, byte[] data)
        {
            var ut = (WSUserToken)(currentObj);
            try
            {
                if (!ut.IsHandSharked)
                {
                    byte[] resData = null;

                    var isHandShark = ut.GetReplayHandShake(data, out resData);

                    if (isHandShark)
                    {
                        _server.SendAsync(ut.ID, resData);
                        ut.IsHandSharked = true;
                        lock (_locker)
                            Clients.Add(ut.ID);
                        OnConnected?.Invoke(ut.ID);
                    }
                }
                else
                {
                    _reader.Insert(ut.ID, data);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("WServer.OnReceive.Error", ex);
            }
        }

        /// <summary>
        /// 回复基础消息
        /// </summary>
        /// <param name="id">客户端ID</param>
        /// <param name="data">协议数据</param>
        private void ReplyBase(string id, byte[] data)
        {
            _writer.Insert(id, data);
        }

        /// <summary>
        /// 回复基础消息
        /// </summary>
        /// <param name="id">客户端ID</param>
        /// <param name="type">协议类型</param>
        /// <param name="content">内容</param>
        private void ReplyBase(string id, WSProtocalType type, byte[] content)
        {
            var bs = new WSProtocal(type, content);

            Reply(id, bs);
        }


        /// <summary>
        /// 回复消息
        /// </summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        public void Reply(string id, WSProtocal data)
        {
            var bs = data.ToBytes(false);
            ReplyBase(id, bs);
        }

        /// <summary>
        /// 回复Pong消息
        /// </summary>
        /// <param name="id">客户端ID</param>
        /// <param name="data">协议数据</param>
        private void ReplyPong(string id, WSProtocal data)
        {
            ReplyBase(id, WSProtocalType.Pong, data.Content);
        }

        /// <summary>
        /// 回复关闭消息
        /// </summary>
        /// <param name="id">客户端ID</param>
        /// <param name="data">协议数据</param>
        private void ReplyClose(string id, WSProtocal data)
        {
            ReplyBase(id, WSProtocalType.Close, data.Content);
        }

        /// <summary>
        /// 发送关闭消息并断开连接
        /// </summary>
        /// <param name="id">客户端ID</param>
        /// <param name="data">协议数据</param>
        public void Disconnect(string id, WSProtocal data)
        {
            Reply(id, data);
            _server.Disconnect(id);
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="id">客户端ID</param>
        public void Disconnect(string id)
        {
            _server.Disconnect(id);
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        /// <param name="backlog">挂起连接队列的最大长度</param>
        public void Start(int backlog = 10000)
        {
            _server.Start(backlog);
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public void Stop()
        {
            _server.Stop();
        }
    }

}
