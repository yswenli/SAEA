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
*命名空间：SAEA.WebSocket.Core
*文件名： WSSServerImpl
*版本号： v26.4.23.1
*唯一标识：938a92d8-d784-4169-a9ea-17a0de5ae367
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/06/14 11:19:42
*描述：WSSServerImpl类
*
*=====================================================================
*修改标记
*修改时间：2019/06/14 11:19:42
*修改人： yswenli
*版本号： v26.4.23.1
*描述：WSSServerImpl类
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Threading;
using SAEA.Sockets;
using SAEA.Sockets.Core;
using SAEA.Sockets.Model;
using SAEA.WebSocket.Model;
using SAEA.WebSocket.Type;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Authentication;

namespace SAEA.WebSocket.Core
{
    internal class WSSServerImpl : IWSServer
    {
        IServerSocket serverSokcet;

        int _bufferSize = 1024;

        public List<string> Clients { set; get; } = new List<string>();

        ConcurrentDictionary<string, WSCoder> _concurrentDictionary;

        public WSSServerImpl(SslProtocols sslProtocols, string pfxPath, string pwd = "", int port = 39654, int bufferSize = 1024 * 64)
        {
            _bufferSize = bufferSize;

            var builder = SocketOptionBuilder.Instance
                .UseStream()
                .SetPort(port)
                .WithSsl(sslProtocols, pfxPath, pwd);

            var options = builder.Build();

            serverSokcet = SocketFactory.CreateServerSocket(options);

            serverSokcet.OnAccepted += ServerSokcet_OnAccepted;

            serverSokcet.OnDisconnected += ServerSokcet_OnDisconnected;

            _concurrentDictionary = new ConcurrentDictionary<string, WSCoder>();
        }

        private void ServerSokcet_OnDisconnected(string id, Exception ex)
        {
            if (string.IsNullOrEmpty(id)) return;
            Clients.Remove(id);
            OnDisconnected?.Invoke(id);
        }

        private void ServerSokcet_OnAccepted(object obj)
        {
            ProcessReceive(obj);
        }

        public event Action<string> OnConnected;

        public event Action<string, WSProtocal> OnMessage;

        public event Action<string> OnDisconnected;

        void ProcessReceive(Object obj)
        {
            TaskHelper.LongRunning(() =>
            {
                var channelInfo = (ChannelInfo)obj;

                if (channelInfo == null) return;

                while (channelInfo.ClientSocket.Connected)
                {
                    try
                    {
                        var data = new byte[_bufferSize];

                        var offset = 0;

                        var size = channelInfo.Stream.Read(data, offset, _bufferSize);

                        while (size > 0)
                        {
                            offset += size;
                            size = channelInfo.Stream.Read(data, offset, _bufferSize);
                        }

                        if (!_concurrentDictionary.ContainsKey(channelInfo.ID))
                        {
                            byte[] resData = null;

                            var wsut = new WSUserToken();

                            var result = wsut.GetReplayHandShake(data, out resData);

                            if (result)
                            {
                                channelInfo.Stream.Write(resData, 0, resData.Length);
                                wsut.IsHandSharked = true;
                                _concurrentDictionary[channelInfo.ID] = new WSCoder();
                                Clients.Add(channelInfo.ID);
                                OnConnected?.Invoke(channelInfo.ID);
                            }
                        }
                        else
                        {
                            var coder = _concurrentDictionary[channelInfo.ID];
                            var msgs = coder.Decode(data);
                            if (msgs == null || msgs.Count < 1) return;
                            foreach (var msg in msgs)
                            {
                                var wsProtocal = (WSProtocal)msg;
                                switch (wsProtocal.Type)
                                {
                                    case (byte)WSProtocalType.Close:
                                        ReplyClose(channelInfo.Stream, wsProtocal);
                                        break;
                                    case (byte)WSProtocalType.Ping:
                                        ReplyPong(channelInfo.Stream, wsProtocal);
                                        break;
                                    case (byte)WSProtocalType.Binary:
                                    case (byte)WSProtocalType.Text:
                                    case (byte)WSProtocalType.Cont:
                                        OnMessage?.Invoke(channelInfo.ID, (WSProtocal)msg);
                                        break;
                                    case (byte)WSProtocalType.Pong:
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
                        LogHelper.Error("WServer.OnReceive.Error", ex);
                        channelInfo.Stream.Close();
                    }
                }
            });
        }


        private void ReplyBase(Stream stream, WSProtocalType type, byte[] content)
        {
            var byts = new WSProtocal(type, content).ToBytes();

            stream.Write(byts, 0, byts.Length);
        }

        private void ReplyBase(Stream stream, WSProtocal data)
        {
            var byts = data.ToBytes();

            stream.Write(byts, 0, byts.Length);
        }

        private void ReplyPong(Stream stream, WSProtocal data)
        {
            ReplyBase(stream, WSProtocalType.Pong, data.Content);
        }

        private void ReplyClose(Stream stream, WSProtocal data)
        {
            ReplyBase(stream, WSProtocalType.Close, data.Content);
        }

        public void Reply(string id, WSProtocal data)
        {
            if (string.IsNullOrEmpty(id)) return;

            var channelInfo = ChannelManager.Instance.Get(id);

            ReplyBase(channelInfo.Stream, data);
        }

        public void Disconnect(string id, WSProtocal data)
        {
            if (string.IsNullOrEmpty(id)) return;

            var channelInfo = ChannelManager.Instance.Get(id);

            ReplyBase(channelInfo.Stream, data);

            channelInfo.Stream.Close();
        }

        public void Disconnect(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var channelInfo = ChannelManager.Instance.Get(id);
            channelInfo.Stream.Close();
        }

        public void Start(int backlog = 10000)
        {
            serverSokcet.Start(backlog);
        }

        public void Stop()
        {
            serverSokcet.Stop();
        }
    }
}
