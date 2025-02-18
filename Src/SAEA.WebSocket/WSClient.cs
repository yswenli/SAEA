/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.WebSocket
*文件名： WSClient
*版本号： v7.0.0.1
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
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using System;
using System.Text;
using System.Threading;

using SAEA.Common;
using SAEA.Sockets;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.WebSocket.Model;
using SAEA.WebSocket.Type;

namespace SAEA.WebSocket
{
    /// <summary>
    /// WSClient
    /// </summary>
    public class WSClient
    {
        string _url;

        string _serverIP;

        int _serverPort;

        bool _isHandSharked = false;

        string _subProtocol = "SAEA.WebSocket";

        string _origin;

        public event OnErrorHandler OnError;

        public event Action<string> OnPong;

        public event Action<WSProtocal> OnMessage;

        public event Action<string> OnClose;

        public event OnDisconnectedHandler OnDisconnected;

        IClientSocket _client;

        WSContext _wsContext;

        /// <summary>
        /// 初始化 WSClient 类的新实例
        /// </summary>
        /// <param name="ip">服务器IP地址</param>
        /// <param name="port">服务器端口</param>
        /// <param name="subProtocol">子协议</param>
        /// <param name="origin">请求来源</param>
        /// <param name="bufferSize">缓冲区大小</param>
        public WSClient(string ip = "127.0.0.1", int port = 39654, string subProtocol = SubProtocolType.Default, string origin = "", int bufferSize = 64 * 1024)
        {
            _serverIP = ip;
            _serverPort = port;

            if (string.IsNullOrEmpty(_url))
                _url = $"ws://{ip}:{port}/";

            _origin = origin;

            _wsContext = new WSContext();

            var option = SocketOptionBuilder.Instance
                .UseIocp(_wsContext)
                .SetIP(ip)
                .SetPort(port)
                .SetReadBufferSize(bufferSize)
                .SetReadBufferSize(bufferSize)
                .Build();

            _subProtocol = subProtocol;

            _client = SocketFactory.CreateClientSocket(option);

            _client.OnReceive += _client_OnReceive;
            _client.OnDisconnected += WSClient_OnDisconnected;
            _client.OnError += WSClient_OnError;
        }

        /// <summary>
        /// 初始化 WSClient 类的新实例
        /// </summary>
        /// <param name="uri">服务器URI</param>
        /// <param name="subProtocol">子协议</param>
        /// <param name="origin">请求来源</param>
        public WSClient(Uri uri, string subProtocol = SubProtocolType.Default, string origin = "") : this(uri.Host, uri.Port, subProtocol, origin)
        {
            _url = uri.ToString();
        }

        /// <summary>
        /// 初始化 WSClient 类的新实例
        /// </summary>
        /// <param name="url">服务器URL</param>
        /// <param name="subProtocol">子协议</param>
        /// <param name="origin">请求来源</param>
        public WSClient(string url, string subProtocol = SubProtocolType.Default, string origin = "") : this(new Uri(url), subProtocol, origin)
        {
            _url = url;
        }

        /// <summary>
        /// 处理接收到的数据
        /// </summary>
        /// <param name="data">接收到的数据</param>
        private void _client_OnReceive(byte[] data)
        {
            OnReceived(data);
        }

        /// <summary>
        /// 处理错误事件
        /// </summary>
        /// <param name="id">客户端ID</param>
        /// <param name="ex">异常信息</param>
        private void WSClient_OnError(string id, Exception ex)
        {
            OnError?.Invoke(id, ex);
        }

        /// <summary>
        /// 处理断开连接事件
        /// </summary>
        /// <param name="id">客户端ID</param>
        /// <param name="ex">异常信息</param>
        private void WSClient_OnDisconnected(string id, Exception ex)
        {
            OnDisconnected?.Invoke(id, ex);
            OnClose?.Invoke(id);
        }

        /// <summary>
        /// 连接WebSocket服务器
        /// </summary>
        /// <param name="timeOut">超时时间</param>
        /// <returns>是否连接成功</returns>
        public bool Connect(int timeOut = 10 * 1000)
        {
            _client.ConnectAsync((e) =>
            {
                _client.SendAsync(WSUserToken.RequestHandShark(_url, _serverIP, _serverPort, _subProtocol, _origin));
            });

            var to = timeOut / 10;

            int i = 0;

            while (!_isHandSharked && i < to)
            {
                Thread.Sleep(10);
                i++;
            }

            if (_isHandSharked) return true;

            _client.Disconnect();

            return false;
        }

        /// <summary>
        /// 处理接收到的数据
        /// </summary>
        /// <param name="data">接收到的数据</param>
        protected void OnReceived(byte[] data)
        {
            try
            {

                if (!_isHandSharked)
                {
                    _isHandSharked = WSUserToken.AnalysisHandSharkReply(data);
                }
                else
                {
                    var coder = (WSCoder)_wsContext.Unpacker;

                    var msgs = coder.Decode(data);

                    if (msgs == null || msgs.Count == 0)
                    {
                        return;
                    }

                    foreach (var msg in msgs)
                    {
                        var wsProtocal = (WSProtocal)msg;
                        switch (wsProtocal.Type)
                        {
                            case (byte)WSProtocalType.Close:
                                _client.Disconnect();
                                break;
                            case (byte)WSProtocalType.Pong:
                                OnPong?.Invoke(Encoding.UTF8.GetString(wsProtocal.Content));
                                break;
                            case (byte)WSProtocalType.Binary:
                            case (byte)WSProtocalType.Text:
                            case (byte)WSProtocalType.Cont:
                                OnMessage?.Invoke((WSProtocal)msg);
                                break;
                            case (byte)WSProtocalType.Ping:
                                ReplyPong();
                                break;
                            default:
                                var error = string.Format("收到未定义的Opcode={0}", msg.Type);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(_client.Context.UserToken.ID, ex);
            }
        }

        /// <summary>
        /// 发送数据到WebSocket服务器
        /// </summary>
        /// <param name="msg">要发送的数据</param>
        public void SendBase(ISocketProtocal msg)
        {
            _client.SendAsync(msg.ToBytes());
        }

        /// <summary>
        /// 发送数据到WebSocket服务器
        /// </summary>
        /// <param name="msg">要发送的数据</param>
        /// <param name="type">数据类型</param>
        public void Send(byte[] msg, WSProtocalType type = WSProtocalType.Text)
        {
            SendBase(new WSProtocal(type, msg));
        }

        /// <summary>
        /// 发送文本数据到WebSocket服务器
        /// </summary>
        /// <param name="msg">要发送的文本数据</param>
        public void Send(string msg)
        {
            Send(Encoding.UTF8.GetBytes(msg));
        }

        /// <summary>
        /// 回复服务器心跳
        /// </summary>
        private void ReplyPong()
        {
            var data = new WSProtocal(WSProtocalType.Pong, null).ToBytes();
            SendBase(new WSProtocal(WSProtocalType.Pong, null));
        }

        /// <summary>
        /// 发送Ping包
        /// </summary>
        public void Ping()
        {
            var msg = DateTimeHelper.ToString();
            SendBase(new WSProtocal(WSProtocalType.Ping, Encoding.UTF8.GetBytes(msg)));
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        /// <param name="discription">关闭描述</param>
        public void Close(string discription = "客户端主动断开ws连接")
        {
            var msg = DateTimeHelper.ToString();
            SendBase(new WSProtocal(WSProtocalType.Close, Encoding.UTF8.GetBytes(discription)));
        }
    }
}
