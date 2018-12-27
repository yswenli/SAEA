/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.TcpP2P
*文件名： PeerSocket
*版本号： V3.6.2.1
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
*版本号： V3.6.2.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.TcpP2P.Net;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SAEA.TcpP2P
{
    /// <summary>
    /// 
    /// </summary>
    public class Peer
    {
        Sender _psSender;

        Sender _p2pSender;

        Receiver _p2pConnect;

        AutoResetEvent _autoResetEvent = new AutoResetEvent(false);

        public NatInfo MyNatInfo { get; private set; }

        public NatInfo RemoteNatInfo { get; private set; }

        public List<NatInfo> PeerList
        {
            get; private set;
        }

        public event Action<NatInfo> OnPublicNatInfoResponse;

        public event Action<NatInfo> OnP2pSucess;

        public event Action<string> OnP2pFailed;

        public event Action<byte[]> OnMessage;

        public event OnDisconnectedHandler OnServerDisconnected;

        public event OnDisconnectedHandler OnP2pDisconnected;

        bool isSenderConnected = false;

        public bool IsConnected
        {
            get; set;
        } = false;



        public void ConnectPeerServer(string ipPort)
        {
            var tuple = ipPort.ToIPPort();
            _psSender = new Sender(tuple.Item1, tuple.Item2);
            _psSender.OnPublicNatInfoResponse += _psSender_OnPublicNatInfoResponse;
            _psSender.OnReceiveP2PTask += _psSender_OnReceiveP2PTask;
            _psSender.OnDisconnected += _psSender_OnDisconnected;
            _psSender.ConnectServer();
        }

        private void _psSender_OnPublicNatInfoResponse(NatInfo obj)
        {
            MyNatInfo = obj;
            OnPublicNatInfoResponse?.Invoke(obj);
        }

        public void RequestPublicNatInfo()
        {
            _psSender.RequestPublicNatInfo();
        }

        private void _psSender_OnDisconnected(string ID, Exception ex)
        {
            OnServerDisconnected?.Invoke(ID, ex);
        }

        public void RequestP2p(string remoteIpPort)
        {
            var ipPort = remoteIpPort.ToIPPort();
            RemoteNatInfo = new NatInfo()
            {
                IP = ipPort.Item1,
                Port = ipPort.Item2,
                IsMe = false
            };
            _psSender.SendP2PRequest(remoteIpPort);
            _autoResetEvent.WaitOne();
        }

        private void HolePunching(string remote)
        {
            var tuple = remote.ToIPPort();
            try
            {
                if (_p2pSender == null || _p2pSender.IsDisposed)
                {
                    _p2pSender = new Sender(tuple.Item1, tuple.Item2);
                    _p2pSender.OnP2PSucess += _p2pSender_OnP2PSucess;
                }
                _p2pSender.HolePunching();
            }
            catch
            {
                _p2pSender.OnP2PSucess -= _p2pSender_OnP2PSucess;
                _p2pSender.Dispose();
                _p2pSender = null;
            }
        }

        private void _p2pSender_OnP2PSucess(NatInfo obj)
        {
            RemoteNatInfo = obj;
            isSenderConnected = true;
            IsConnected = true;
            OnP2pSucess?.Invoke(RemoteNatInfo);
            _autoResetEvent.Set();
        }


        private void _psSender_OnReceiveP2PTask(NatInfo obj)
        {
            _p2pConnect = new Receiver(false);
            _p2pConnect.OnP2PSucess += _p2pConnect_OnP2PSucess;
            _p2pConnect.OnMessage += _p2pConnect_OnMessage;
            _p2pConnect.OnDisconnected += _p2pConnect_OnDisconnected;
            _p2pConnect.Start(MyNatInfo.Port);

            _psSender.Dispose();

            TaskHelper.Start(() =>
            {
                int tryCount = 0;

                int maxCount = 300;

                while (!IsConnected)
                {
                    ThreadHelper.Sleep(100);
                    tryCount++;
                    HolePunching(obj.ToString());
                    if (tryCount > maxCount && !IsConnected)
                    {
                        OnP2pFailed?.Invoke($"已重试超过{maxCount}次");
                        _autoResetEvent.Set();
                        break;
                    }
                }
            });
        }

        private void _p2pConnect_OnP2PSucess(NatInfo obj)
        {
            IsConnected = true;
            RemoteNatInfo = obj;
            OnP2pSucess?.Invoke(RemoteNatInfo);
            _autoResetEvent.Set();
        }

        private void _p2pConnect_OnDisconnected(string ID, Exception ex)
        {
            OnP2pDisconnected?.Invoke(ID, ex);
        }

        private void _p2pConnect_OnMessage(IUserToken arg1, ISocketProtocal arg2)
        {
            if (arg2.Content != null) OnMessage?.Invoke(arg2.Content);
        }

        private void _p2pSender_OnMessage(Sockets.Interface.ISocketProtocal obj)
        {
            if (obj.Content != null) OnMessage?.Invoke(obj.Content);
        }

        public void SendMessage(byte[] bytes)
        {
            if (IsConnected)
            {
                if (isSenderConnected)
                {
                    _p2pSender.SendMessage(bytes);
                }
                else
                {
                    _p2pConnect.SendMessage(RemoteNatInfo.ToString(), bytes);
                }
            }
        }

        public void SendMessage(string msg)
        {
            SendMessage(Encoding.UTF8.GetBytes(msg));
        }
    }
}
