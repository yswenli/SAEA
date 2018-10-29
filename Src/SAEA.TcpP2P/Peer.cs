/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.TcpP2P
*文件名： Peer
*版本号： V3.1.0.0
*唯一标识：1f88c4b4-a22d-4e98-a403-16c5e911ef86
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 22:44:06
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 22:44:06
*修改人： yswenli
*版本号： V3.1.0.0
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Interface;
using SAEA.TcpP2P.Net;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.TcpP2P
{
    public class Peer
    {
        Sender _sender;

        Receiver _receiver;


        public event Action<Peer> OnConnected;

        public event Action<string> OnMessage;

        Tuple<string, int> _peerBIPPort;

        public bool Connected { get; set; } = false;

        public Peer(string server)
        {
            var ipPort = server.GetIPPort();

            _sender = new Sender(ipPort.Item1, ipPort.Item2);

            _sender.OnReceiveP2PTask += _sender_OnReceiveP2PTask;

            _sender.OnP2PSucess += _sender_OnP2PSucess;

            _sender.OnMessage += _sender_OnMessage;

            _sender.ConnectServer();

            _receiver = new Receiver();

            _receiver.OnP2PSucess += _receiver_OnP2PSucess;

            _receiver.OnMessage += _receiver_OnMessage;
        }



        private void _sender_OnReceiveP2PTask(Tuple<string, int> obj)
        {
            _receiver.Start(obj.Item2);
        }

        private void _sender_OnP2PSucess(Tuple<string, int> obj)
        {
            Connected = true;
            OnConnected?.Invoke(this);
            _peerBIPPort = obj;
        }

        private void _sender_OnMessage(Sockets.Interface.ISocketProtocal msg)
        {
            OnMessage?.Invoke(Encoding.UTF8.GetString(msg.Content));
        }

        private void _receiver_OnP2PSucess(Tuple<string, int> obj)
        {
            Connected = true;
            OnConnected?.Invoke(this);
            _peerBIPPort = obj;
        }
        private void _receiver_OnMessage(Sockets.Interface.IUserToken userToken, Sockets.Interface.ISocketProtocal msg)
        {
            OnMessage?.Invoke(Encoding.UTF8.GetString(msg.Content));
        }

        public void RequestP2P(string remote)
        {
            _sender.SendP2PRequest(remote);
        }

        public void SendMessage(string msg)
        {
            if (Connected)
                _sender.SendMessage(msg);
        }

    }
}
