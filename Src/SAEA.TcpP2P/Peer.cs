/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.TcpP2P
*文件名： PeerSocket
*版本号： V3.3.3.4
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
*版本号： V3.3.3.4
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Sockets.Interface;
using SAEA.TcpP2P.Net;
using System;
using System.Collections.Generic;

namespace SAEA.TcpP2P
{
    /// <summary>
    /// 
    /// </summary>
    public class Peer
    {
        Sender _psSender;

        Receiver _p2pConnect;

        IUserToken rUserToken;

        public event Action<List<string>> OnPeerListResponse;

        public event Action<Tuple<string, int>> OnP2PSucess;

        public event Action<byte[]> OnMessage;

        public bool IsConnected
        {
            get; set;
        }

        public void ConnectPeerServer(string ipPort)
        {
            var tuple = ipPort.GetIPPort();
            _psSender = new Sender(tuple.Item1, tuple.Item2);
            _psSender.OnPeerListResponse += _psSender_OnPeerListResponse;
            _psSender.OnReceiveP2PTask += _psSender_OnReceiveP2PTask;
            _psSender.ConnectServer();
        }


        public void RequestP2p(string remoteIpPort)
        {
            _psSender.SendP2PRequest(remoteIpPort);
        }

        private void _psSender_OnPeerListResponse(List<string> obj)
        {
            OnPeerListResponse?.Invoke(obj);
        }

        private void _psSender_OnReceiveP2PTask(Tuple<string, int> obj)
        {
            _p2pConnect = new Receiver(obj.Item1, obj.Item2);
            _p2pConnect.OnP2PSucess += _p2pSender_OnP2PSucess;
            _p2pConnect.OnMessage += _p2pConnect_OnMessage;
            _p2pConnect.HolePunching();
        }

        private void _p2pConnect_OnMessage(IUserToken arg1, ISocketProtocal arg2)
        {
            if (arg2.Content != null) OnMessage?.Invoke(arg2.Content);
        }

        private void _p2pSender_OnMessage(Sockets.Interface.ISocketProtocal obj)
        {
            if (obj.Content != null) OnMessage?.Invoke(obj.Content);
        }

        private void _p2pSender_OnP2PSucess(Tuple<string, int> obj)
        {
            IsConnected = true;
            OnP2PSucess?.Invoke(obj);
        }

        public void SendMessage(byte[] bytes)
        {
            if (IsConnected)
                _p2pConnect.SendBase(HolePunchingType.Message, bytes);
        }

        public void SendMessage(string msg)
        {
            if (IsConnected)
                _p2pConnect.SendMessage(msg);
        }
    }
}
