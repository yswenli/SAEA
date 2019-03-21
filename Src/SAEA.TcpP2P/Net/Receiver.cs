/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.TcpP2P.Net
*文件名： Receiver
*版本号： v4.3.1.2
*唯一标识：02774aed-635d-4731-82ec-daaace9ce96f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 21:46:22
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 21:46:22
*修改人： yswenli
*版本号： v4.3.1.2
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Interface;
using System;
using System.Text;

namespace SAEA.TcpP2P.Net
{
    public class Receiver : IocpServerSocket
    {
        public event Action<NatInfo> OnP2PSucess;

        public event Action<IUserToken, ISocketProtocal> OnMessage;

        bool _isP2pServer = false;

        public Receiver(bool isP2pServer = false) : base(new PContext(), 1024, (isP2pServer ? 10000 : 1))
        {
            _isP2pServer = isP2pServer;
        }

        protected override void OnReceiveBytes(IUserToken userToken, byte[] data)
        {
            userToken.Unpacker.Unpack(data, (msg) =>
            {
                switch (msg.Type)
                {
                    case (byte)TcpP2pType.Heart:
                        ReplyHeart(userToken);
                        break;
                    case (byte)TcpP2pType.PublicNatInfoRequest:
                        ReplyPeerListRequest(userToken);
                        break;
                    case (byte)TcpP2pType.P2PSRequest:
                        ReplyP2PSRequest(userToken, msg);
                        break;
                    case (byte)TcpP2pType.P2PResponse:
                        var ipPort = userToken.Socket.RemoteEndPoint.ToString().ToIPPort();
                        var natInfo = new NatInfo()
                        {
                            IP = ipPort.Item1,
                            Port = ipPort.Item2,
                            IsMe = false
                        };
                        OnP2PSucess?.Invoke(natInfo);
                        ReplyP2PResponse(userToken);
                        break;
                    case (byte)TcpP2pType.Message:
                        OnMessage?.Invoke(userToken, msg);
                        break;
                    case (byte)TcpP2pType.Logout:
                    case (byte)TcpP2pType.Close:
                    default:
                        base.Disconnect(userToken, new Exception("收到来自客户端的关闭请求"));
                        break;
                }
            }, null, null);
        }

        private void ReplyBase(IUserToken userToken, PSocketMsg msg)
        {
            base.SendAsync(userToken, msg.ToBytes());
        }

        private void ReplyHeart(IUserToken userToken)
        {
            ReplyBase(userToken, PSocketMsg.Parse(null, TcpP2pType.Heart));
        }

        private void ReplyP2PResponse(IUserToken userToken)
        {
            ReplyBase(userToken, PSocketMsg.Parse(null, TcpP2pType.P2PResponse));
        }

        private void ReplyPeerListRequest(IUserToken userToken)
        {

            var ipPort = userToken.ID.ToIPPort();

            var natInfo = new NatInfo()
            {
                IP = ipPort.Item1,
                Port = ipPort.Item2,
                IsMe = true
            };

            var data = SerializeHelper.ByteSerialize(natInfo);

            ReplyBase(userToken, PSocketMsg.Parse(data, TcpP2pType.PublicNatInfoResponse));
        }

        /// <summary>
        /// 收到peerA的申请，回复两客户端remote
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="msg"></param>
        private void ReplyP2PSRequest(IUserToken userToken, ISocketProtocal msg)
        {
            var peerA = userToken.ID.ToIPPort();

            var natInfoA = new NatInfo()
            {
                IP = peerA.Item1,
                Port = peerA.Item2,
                IsMe = false
            };

            var IDB = Encoding.UTF8.GetString(msg.Content);

            var peerB = IDB.ToIPPort();

            var natInfoB = new NatInfo()
            {
                IP = peerB.Item1,
                Port = peerB.Item2,
                IsMe = false
            };

            base.SendAsync(IDB, PSocketMsg.Parse(SerializeHelper.ByteSerialize(natInfoA), TcpP2pType.P2PSResponse).ToBytes());
            base.SendAsync(userToken.ID, PSocketMsg.Parse(SerializeHelper.ByteSerialize(natInfoB), TcpP2pType.P2PSResponse).ToBytes());
        }

        public void SendMessage(string ipPort, byte[] msg)
        {
            base.SendAsync(ipPort, PSocketMsg.Parse(msg, TcpP2pType.Message).ToBytes());
        }
    }
}
