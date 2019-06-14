/****************************************************************************
*项目名称：SAEA.WebSocket.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.WebSocket.Core
*类 名 称：WSSServerImpl
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/13 15:44:53
*描述：
*=====================================================================
*修改时间：2019/6/13 15:44:53
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.Sockets;
using SAEA.Sockets.Core;
using SAEA.Sockets.Interface;
using SAEA.WebSocket.Model;
using SAEA.WebSocket.Type;
using System;
using System.IO;
using System.Security.Authentication;

namespace SAEA.WebSocket.Core
{
    internal class WSSServerImpl : IWSServer
    {
        IServerSokcet serverSokcet;

        int _bufferSize = 1024;

        public WSSServerImpl(SslProtocols sslProtocols, string pfxPath, string pwd = "", int port = 39654, int bufferSize = 1024)
        {
            _bufferSize = bufferSize;

            var builder = SocketOptionBuilder.Instance.UseStream().SetPort(port).WithSsl(sslProtocols, pfxPath, pwd);

            var options = builder.Build();

            serverSokcet = SocketFactory.CreateServerSocket(options);

            serverSokcet.OnAccepted += ServerSokcet_OnAccepted;
        }

        private void ServerSokcet_OnAccepted(object userToken)
        {
            ProcessReceive(userToken);
        }

        public event Action<string, WSProtocal> OnMessage;


        void ProcessReceive(object userToken)
        {
            TaskHelper.Start(() =>
            {
                var ut = userToken as WSUserToken;

                if (ut == null) return;

                var channelInfo = ChannelManager.Current.Get(ut.ID);

                if (channelInfo == null) return;

                while (channelInfo.ClientSocket.Connected)
                {
                    var data = new byte[_bufferSize];

                    var offset = 0;

                    var size = channelInfo.Stream.Read(data, offset, _bufferSize);

                    while (size > 0)
                    {
                        offset += size;
                        size = channelInfo.Stream.Read(data, offset, _bufferSize);
                    }

                    if (!ut.IsHandSharked)
                    {
                        byte[] resData = null;

                        var result = ut.GetReplayHandShake(data, out resData);

                        if (result)
                        {
                            channelInfo.Stream.Write(resData, 0, resData.Length);
                            ut.IsHandSharked = true;
                        }
                    }
                    else
                    {
                        var coder = (WSCoder)ut.Unpacker;
                        coder.Unpack(data, (d) =>
                        {
                            var wsProtocal = (WSProtocal)d;
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
                                    OnMessage?.Invoke(ut.ID, (WSProtocal)d);
                                    break;
                                case (byte)WSProtocalType.Pong:
                                    break;
                                default:
                                    var error = string.Format("收到未定义的Opcode={0}", d.Type);
                                    break;
                            }

                        }, (h) => { }, null);
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
            var channelInfo = ChannelManager.Current.Get(id);

            ReplyBase(channelInfo.Stream, data);
        }

        public void Disconnect(string id, WSProtocal data)
        {
            var channelInfo = ChannelManager.Current.Get(id);

            ReplyBase(channelInfo.Stream, data);
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
