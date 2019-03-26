/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.WebSocket
*文件名： Class1
*版本号： v4.3.2.5
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
*版本号： v4.3.2.5
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Core.Tcp;
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
    public class WSClient : IocpClientSocket
    {
        int _bufferSize = 100 * 1024;

        byte[] _buffer;

        string _serverIP;

        int _serverPort;

        bool _isHandSharked = false;

        List<byte> _cache = new List<byte>();

        public new event OnErrorHandler OnError;

        public event Action<DateTime> OnPong;

        public event Action<WSProtocal> OnMessage;

        public event Action<string> OnClose;

        public WSClient(string ip = "127.0.0.1", int port = 39654, int bufferSize = 10 * 1024) : base(new WSContext(), ip, port, bufferSize)
        {
            _serverIP = ip;
            _serverPort = port;
            _bufferSize = bufferSize;
            _buffer = new byte[_bufferSize];
            base.OnDisconnected += WSClient_OnDisconnected;
            base.OnError += WSClient_OnError;
        }

        private void WSClient_OnError(string ID, Exception ex)
        {
            OnError?.Invoke(ID, ex);
        }

        private void WSClient_OnDisconnected(string ID, Exception ex)
        {
            OnClose?.Invoke(ex.Message);
        }

        /// <summary>
        /// 连接websocketServer
        /// </summary>
        /// <returns></returns>
        public bool Connect(int timeOut = 10 * 1000)
        {
            base.ConnectAsync((se) =>
            {
                if (se == System.Net.Sockets.SocketError.Success)
                {
                    this.RequestHandShark();
                }
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

        protected override void OnReceived(byte[] data)
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
                    OnError.Invoke(UserToken.ID, ex);
                }
            }
            else
            {
                var coder = (WSCoder)UserToken.Unpacker;

                coder.Unpack(data, (d) =>
                {
                    var wsProtocal = (WSProtocal)d;
                    switch (wsProtocal.Type)
                    {
                        case (byte)WSProtocalType.Close:
                            base.Disconnect();
                            break;
                        case (byte)WSProtocalType.Pong:
                            var date = DateTime.Parse(Encoding.UTF8.GetString(wsProtocal.Content));
                            OnPong?.Invoke(date);
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
            base.Send(WSUserToken.RequestHandShark(_serverIP, _serverPort));
        }

        /// <summary>
        /// 发送数据到wsserver
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="type"></param>
        public void Send(byte[] msg, WSProtocalType type = WSProtocalType.Text)
        {
            var data = new WSProtocal(type, msg).ToBytes();
            base.Send(data);
        }

        /// <summary>
        /// 发送文本到wsserver
        /// </summary>
        /// <param name="msg"></param>
        public void Send(string msg)
        {
            this.Send(Encoding.UTF8.GetBytes(msg));
        }

        /// <summary>
        /// 回复服务器心跳
        /// </summary>
        private void ReplyPong()
        {
            var msg = DateTimeHelper.ToString();
            var data = new WSProtocal(WSProtocalType.Pong, null).ToBytes();
            base.Send(data);
        }


        /// <summary>
        /// 发送ping包
        /// </summary>
        public void Ping()
        {
            var msg = DateTimeHelper.ToString();
            var data = new WSProtocal(WSProtocalType.Ping, Encoding.UTF8.GetBytes(msg)).ToBytes();
            base.Send(data);
        }

        public void Close(string discription = "客户端主动断开ws连接")
        {
            var msg = DateTimeHelper.ToString();
            var data = new WSProtocal(WSProtocalType.Close, Encoding.UTF8.GetBytes(discription)).ToBytes();
            base.Send(data);
        }


    }
}
