/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.TcpP2P.Net
*文件名： Receiver
*版本号： V3.1.0.0
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
*版本号： V3.1.0.0
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Core;
using SAEA.Sockets.Interface;
using System;
using System.Linq;
using System.Text;

namespace SAEA.TcpP2P.Net
{
    public class Receiver : BaseServerSocket
    {
        public event Action<Tuple<string, int>> OnP2PSucess;

        public event Action<IUserToken, ISocketProtocal> OnMessage;

        public Receiver() : base(new PContext())
        {

        }

        protected override void OnReceiveBytes(IUserToken userToken, byte[] data)
        {
            userToken.Unpacker.Unpack(data, (msg) =>
            {
                switch (msg.Type)
                {
                    case (byte)HolePunchingType.Heart:
                        ReplyHeart(userToken);
                        break;
                    case (byte)HolePunchingType.Login:
                        ReplyLogin(userToken);
                        break;
                    case (byte)HolePunchingType.P2PSRequest:
                        ReplyP2PSRequest(userToken, msg);
                        break;
                    case (byte)HolePunchingType.P2PResponse:
                        OnP2PSucess?.Invoke(userToken.Socket.RemoteEndPoint.ToString().GetIPPort());
                        ReplyP2PResponse(userToken);
                        break;
                    case (byte)HolePunchingType.Message:
                        OnMessage?.Invoke(userToken, msg);
                        break;
                    case (byte)HolePunchingType.Logout:
                    case (byte)HolePunchingType.Close:
                    default:
                        base.Disconnect(userToken, new Exception("未定义的类型"));
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
            ReplyBase(userToken, PSocketMsg.Parse(null, HolePunchingType.Heart));
        }

        private void ReplyP2PResponse(IUserToken userToken)
        {
            ReplyBase(userToken, PSocketMsg.Parse(null, HolePunchingType.P2PResponse));
        }

        private void ReplyLogin(IUserToken userToken)
        {
            var list = SessionManager.ToList().Select(b => b.ID).ToList();
            if (list != null && list.Count > 0)
            {
                var data = SerializeHelper.ByteSerialize(list);

                ReplyBase(userToken, PSocketMsg.Parse(data, HolePunchingType.LoginResponse));
            }
        }

        /// <summary>
        /// 收到peerA的申请，回复两客户端remote
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="msg"></param>
        private void ReplyP2PSRequest(IUserToken userToken, ISocketProtocal msg)
        {
            var peerA = userToken.ID;

            var peerB = Encoding.UTF8.GetString(msg.Content);

            base.SendAsync(peerA, PSocketMsg.Parse(Encoding.UTF8.GetBytes(peerB), HolePunchingType.P2PSResponse).ToBytes());

            base.SendAsync(peerB, PSocketMsg.Parse(Encoding.UTF8.GetBytes(peerA), HolePunchingType.P2PSResponse).ToBytes());
        }


    }
}
