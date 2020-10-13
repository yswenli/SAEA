/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.WebSocket
*文件名： WSClient
*版本号： v5.0.0.1
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
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets;
using SAEA.Sockets.Handler;
using SAEA.WebSocket.Model;
using SAEA.WebSocket.Type;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace SAEA.WebSocket
{
    /// <summary>
    /// WSClient
    /// </summary>
    public class WSClient
    {
        int _bufferSize = 100 * 1024;

        byte[] _buffer;

        string _url;

        string _serverIP;

        int _serverPort;

        bool _isHandSharked = false;

        string _subProtocol = "SAEA.WebSocket";

        string _origin;

        List<byte> _cache = new List<byte>();

        public event OnErrorHandler OnError;

        public event Action<string> OnPong;

        public event Action<WSProtocal> OnMessage;

        public event Action<string> OnClose;

        public event OnDisconnectedHandler OnDisconnected;

        IClientSocket _client;

        WSContext _wsContext;

        /// <summary>
        /// WSClient
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="subProtocol"></param>
        /// <param name="origin"></param>
        /// <param name="bufferSize"></param>
        public WSClient(string ip = "127.0.0.1", int port = 16666, string subProtocol = SubProtocolType.Default, string origin = "", int bufferSize = 10 * 1024)
        {
            _serverIP = ip;
            _serverPort = port;

            if (string.IsNullOrEmpty(_url))
                _url = $"ws://{ip}:{port}/";

            _origin = origin;

            _bufferSize = bufferSize;

            _buffer = new byte[_bufferSize];

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

        public WSClient(Uri uri, string subProtocol = SubProtocolType.Default, string origin = "") : this(uri.Host, uri.Port, subProtocol, origin)
        {
            _url = uri.ToString();
        }

        public WSClient(string url, string subProtocol = SubProtocolType.Default, string origin = "") : this(new Uri(url), subProtocol, origin)
        {
            _url = url;
        }


        private void _client_OnReceive(byte[] data)
        {
            OnReceived(data);
        }

        private void WSClient_OnError(string id, Exception ex)
        {
            OnError?.Invoke(id, ex);
        }

        private void WSClient_OnDisconnected(string id, Exception ex)
        {
            OnDisconnected?.Invoke(id, ex);
            OnClose?.Invoke(id);
        }

        /// <summary>
        /// 连接websocketServer
        /// </summary>
        /// <returns></returns>
        public bool Connect(int timeOut = 10 * 1000)
        {
            _client.ConnectAsync((e) =>
            {
                this.RequestHandShark();
            });

            int i = 0;

            while (!_isHandSharked && i < (timeOut / 10))
            {
                Thread.Sleep(10);
                i++;
            }
            if (_isHandSharked) return true;

            return false;
        }

        protected void OnReceived(byte[] data)
        {
            if (!_isHandSharked)
            {
                _cache.AddRange(data);

                try
                {
                    var handShakeText = Encoding.UTF8.GetString(_cache.ToArray());
                    string key = string.Empty;
                    Regex reg = new Regex(@"Sec\-WebSocket\-Accept:(.*?)\r\n");
                    Match m = reg.Match(handShakeText);
                    if (string.IsNullOrEmpty(m.Value)) throw new Exception("回复中不存在 Sec-WebSocket-Accept");
                    key = Regex.Replace(m.Value, @"Sec\-WebSocket\-Accept:(.*?)\r\n", "$1").Trim();
                    _isHandSharked = true;
                }
                catch (Exception ex)
                {
                    OnError.Invoke(_wsContext.UserToken.ID, ex);
                }
            }
            else
            {
                var coder = (WSCoder)_wsContext.Unpacker;

                coder.Unpack(data, (d) =>
                {
                    var wsProtocal = (WSProtocal)d;
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
                            OnMessage?.Invoke((WSProtocal)d);
                            break;
                        case (byte)WSProtocalType.Ping:
                            ReplyPong();
                            break;
                        default:
                            var error = string.Format("收到未定义的Opcode={0}", d.Type);
                            break;
                    }

                }, null, null);
            }
        }

        private void RequestHandShark()
        {
            _client.SendAsync(WSUserToken.RequestHandShark(_url, _serverIP, _serverPort, _subProtocol, _origin));
        }

        /// <summary>
        /// 发送数据到wsserver
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="type"></param>
        public void Send(byte[] msg, WSProtocalType type = WSProtocalType.Text)
        {
            var data = new WSProtocal(type, msg).ToBytes();
            _client.SendAsync(data);
        }

        /// <summary>
        /// 发送文本到wsserver
        /// </summary>
        /// <param name="msg"></param>
        public void Send(string msg)
        {
            Send(Encoding.UTF8.GetBytes(msg));
        }

        /// <summary>
        /// 回复服务器心跳
        /// </summary>
        private void ReplyPong()
        {
            var msg = DateTimeHelper.ToString();
            var data = new WSProtocal(WSProtocalType.Pong, null).ToBytes();
            _client.SendAsync(data);
        }


        /// <summary>
        /// 发送ping包
        /// </summary>
        public void Ping()
        {
            var msg = DateTimeHelper.ToString();
            var data = new WSProtocal(WSProtocalType.Ping, Encoding.UTF8.GetBytes(msg)).ToBytes();
            _client.SendAsync(data);
        }

        public void Close(string discription = "客户端主动断开ws连接")
        {
            var msg = DateTimeHelper.ToString();
            var data = new WSProtocal(WSProtocalType.Close, Encoding.UTF8.GetBytes(discription)).ToBytes();
            _client.SendAsync(data);
        }


    }
}
