/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.TcpP2P.Net
*文件名： Sender
*版本号： V3.3.3.4
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
*版本号： V3.3.3.4
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Core;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

namespace SAEA.TcpP2P.Net
{
    public class Sender : BaseClientSocket
    {
        public event Action<Tuple<string, int>> OnReceiveP2PTask;

        public event Action<Tuple<string, int>> OnP2PSucess;

        public event Action<ISocketProtocal> OnMessage;

        public event Action<List<string>> OnPeerListResponse;


        int HeartSpan = 10 * 1000;

        private DateTime Actived;

        Tuple<string, int> _peerBAddress;

        public Sender(string ip, int port) : base(new PContext(), ip, port)
        {

        }

        protected override void OnReceived(byte[] data)
        {
            base.UserToken.Unpacker.Unpack(data, (msg) =>
            {
                switch (msg.Type)
                {
                    case (byte)HolePunchingType.Heart:
                        break;
                    case (byte)HolePunchingType.PeerListRequest:
                        break;
                    case (byte)HolePunchingType.PeerListResponse:
                        var remoteList = SerializeHelper.ByteDeserialize<List<string>>(msg.Content);
                        OnPeerListResponse.Invoke(remoteList);
                        break;
                    case (byte)HolePunchingType.Message:
                        OnMessage?.Invoke(msg);
                        break;
                    case (byte)HolePunchingType.P2PSResponse:
                        var ipPort = Encoding.UTF8.GetString(msg.Content).GetIPPort();
                        OnReceiveP2PTask?.Invoke(ipPort);
                        break;
                    case (byte)HolePunchingType.P2PResponse:
                        OnP2PSucess?.Invoke(_peerBAddress);
                        break;
                    case (byte)HolePunchingType.Logout:
                    case (byte)HolePunchingType.Close:
                    default:
                        SendClose();
                        base.Disconnect();
                        break;
                }
            }, null, null);
        }



        public void SendBase(HolePunchingType type, byte[] content = null)
        {
            var qm = PSocketMsg.Parse(content, type);

            SendAsync(qm.ToBytes());
        }

        private void HeartLoop()
        {
            TaskHelper.Start(() =>
            {
                Actived = DateTimeHelper.Now;
                try
                {
                    while (true)
                    {
                        if (this.Connected)
                        {
                            if (Actived.AddMilliseconds(HeartSpan) <= DateTimeHelper.Now)
                            {
                                SendBase(HolePunchingType.Heart);
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

        public void RequestPeerList()
        {
            var qm = PSocketMsg.Parse(null, HolePunchingType.PeerListRequest);

            Send(qm.ToBytes());
        }

        public void SendClose()
        {
            var qm = PSocketMsg.Parse(null, HolePunchingType.Close);

            Send(qm.ToBytes());
        }

        public void ConnectServer()
        {
            base.ConnectAsync((state) =>
            {
                if (state == System.Net.Sockets.SocketError.Success)
                {
                    HeartLoop();
                    Connected = true;
                    RequestPeerList();
                }
            });
        }

        /// <summary>
        /// 向服务器发送p2p申请
        /// </summary>
        /// <param name="remote"></param>
        public void SendP2PRequest(string remote)
        {
            SendBase(HolePunchingType.P2PSRequest, Encoding.UTF8.GetBytes(remote));
        }

        public void SendMessage(string msg)
        {
            var qm = PSocketMsg.Parse(Encoding.UTF8.GetBytes(msg), HolePunchingType.Message);
            Send(qm.ToBytes());
        }

    }
}
