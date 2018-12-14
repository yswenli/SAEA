/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.TcpP2P.Net
*文件名： Sender
*版本号： V3.5.9.1
*唯一标识：1ea6636f-88e7-4577-be6e-2934d7388893
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 22:18:18
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 22:18:18
*修改人： yswenli
*版本号： V3.5.9.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Core;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace SAEA.TcpP2P.Net
{
    public class Sender : BaseClientSocket
    {
        public event Action<NatInfo> OnReceiveP2PTask;

        public event Action<NatInfo> OnP2PSucess;

        public event Action<ISocketProtocal> OnMessage;

        public event Action<NatInfo> OnPublicNatInfoResponse;


        int HeartSpan = 30 * 1000;

        private DateTime Actived;

        NatInfo _me;

        public Sender(string ip, int port) : base(new PContext(), ip, port)
        {
            
        }

        protected override void OnReceived(byte[] data)
        {
            Actived = DateTime.Now;
            base.UserToken.Unpacker.Unpack(data, (msg) =>
            {
                switch (msg.Type)
                {
                    case (byte)TcpP2pType.Heart:
                        break;
                    case (byte)TcpP2pType.PublicNatInfoRequest:
                        break;
                    case (byte)TcpP2pType.PublicNatInfoResponse:
                        _me = SerializeHelper.ByteDeserialize<NatInfo>(msg.Content);
                        OnPublicNatInfoResponse.Invoke(_me);
                        break;
                    case (byte)TcpP2pType.Message:
                        OnMessage?.Invoke(msg);
                        break;
                    case (byte)TcpP2pType.P2PSResponse:                        
                        OnReceiveP2PTask?.Invoke(SerializeHelper.ByteDeserialize<NatInfo>(msg.Content));
                        break;
                    case (byte)TcpP2pType.P2PResponse:
                        OnP2PSucess?.Invoke(_me);
                        break;
                    case (byte)TcpP2pType.Logout:
                    case (byte)TcpP2pType.Close:
                    default:
                        SendClose();
                        base.Disconnect();
                        break;
                }
            }, null, null);
        }



        public void SendBase(TcpP2pType type, byte[] content = null)
        {
            var qm = PSocketMsg.Parse(content, type);

            SendAsync(qm.ToBytes());

            Actived = DateTime.Now;
        }

        private void HeartLoop()
        {
            TaskHelper.Start(() =>
            {
                Actived = DateTimeHelper.Now;
                try
                {
                    while (!IsDisposed)
                    {
                        if (this.Connected)
                        {
                            if (Actived.AddMilliseconds(HeartSpan) <= DateTimeHelper.Now)
                            {
                                SendBase(TcpP2pType.Heart);
                            }
                            Thread.Sleep(HeartSpan);
                        }
                        else
                        {
                            Thread.Sleep(1);
                        }
                    }
                }
                catch { }
            });
        }

        public void RequestPublicNatInfo()
        {
            var qm = PSocketMsg.Parse(null, TcpP2pType.PublicNatInfoRequest);

            Send(qm.ToBytes());

            Actived = DateTime.Now;
        }

        /// <summary>
        /// 向打洞的远程机器发送数据
        /// </summary>
        public void HolePunching()
        {
            var qm = PSocketMsg.Parse(null, TcpP2pType.P2PRequest);

            Send(qm.ToBytes());

            Actived = DateTime.Now;
        }

        public void SendClose()
        {
            var qm = PSocketMsg.Parse(null, TcpP2pType.Close);

            Send(qm.ToBytes());

            Actived = DateTime.Now;
        }

        public void ConnectServer()
        {
            base.ConnectAsync((state) =>
            {
                if (state == System.Net.Sockets.SocketError.Success)
                {
                    HeartLoop();
                    Connected = true;
                }
            });
        }

        /// <summary>
        /// 向服务器发送p2p申请
        /// </summary>
        /// <param name="remote"></param>
        public void SendP2PRequest(string remote)
        {
            SendBase(TcpP2pType.P2PSRequest, Encoding.UTF8.GetBytes(remote));
            Actived = DateTime.Now;
        }

        public void SendMessage(byte[] msg)
        {
            var qm = PSocketMsg.Parse(msg, TcpP2pType.Message);
            Send(qm.ToBytes());
            Actived = DateTime.Now;
        }
        public void SendMessage(string msg)
        {
            SendMessage(Encoding.UTF8.GetBytes(msg));
            Actived = DateTime.Now;
        }
    }
}
