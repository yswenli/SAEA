﻿/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.QueueSocket
*文件名： QServer
*版本号： v7.0.0.1
*唯一标识：3d6821bb-82f1-4181-8de3-a31341caad45
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 16:16:26
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 16:16:26
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using System;
using System.Collections.Generic;

using SAEA.Common;
using SAEA.Common.Threading;
using SAEA.QueueSocket.Model;
using SAEA.QueueSocket.Net;
using SAEA.QueueSocket.Type;
using SAEA.Sockets;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;

namespace SAEA.QueueSocket
{
    public class QServer
    {
        Exchange _exchange;

        IServerSocket _serverSokcet;

        public event OnDisconnectedHandler OnDisconnected;

        public QServer(int port = 39654, string ip = "127.0.0.1", int bufferSize = 100 * 1024, int count = 100)
        {
            _exchange = new Exchange();

            _exchange.OnBatched += _exchange_OnBatched;

            var config = SocketOptionBuilder.Instance
                .UseIocp<Net.QueueCoder>()
                .SetSocket(Sockets.Model.SAEASocketType.Tcp)
                .SetReadBufferSize(bufferSize)
                .SetWriteBufferSize(bufferSize)
                .SetCount(count)
                .SetIP(ip)
                .SetPort(port)
                .Build();

            _serverSokcet = SocketFactory.CreateServerSocket(config);

            _serverSokcet.OnReceive += _serverSokcet_OnReceive;

            _serverSokcet.OnDisconnected += _serverSokcet_OnDisconnected;
        }

        private void _exchange_OnBatched(string id, byte[] data)
        {
            _serverSokcet.Send(id, data);
        }

        private void _serverSokcet_OnDisconnected(string ID, Exception ex)
        {
            OnDisconnected?.Invoke(ID, ex);
        }

        private void _serverSokcet_OnReceive(ISession ut, byte[] data)
        {
            var userToken = (IUserToken)ut;
            var qcoder = (Net.QueueCoder)userToken.Coder;
            var list = qcoder.GetQueueResult(data);
            if (list != null && list.Count > 0)
            {
                foreach (var item in list)
                {
                    Reply(userToken, item);
                }
                list.Clear();
            }
        }

        void Reply(IUserToken userToken, QueueMsg queueResult)
        {
            switch (queueResult.Type)
            {
                case QueueSocketMsgType.Ping:
                    ReplyPong(userToken, queueResult);
                    break;
                case QueueSocketMsgType.Publish:
                    ReplyPublish(userToken, queueResult);
                    break;
                case QueueSocketMsgType.Subcribe:
                    ReplySubcribe(userToken, queueResult);
                    break;
                case QueueSocketMsgType.Unsubcribe:
                    ReplyUnsubscribe(userToken, queueResult);
                    break;
                case QueueSocketMsgType.Close:
                    ReplyClose(userToken, queueResult);
                    break;
            }
        }


        public void Start(int backlog = 10 * 1000)
        {
            _serverSokcet.Start(backlog);

            _calcBegin = true;
        }


        public void Stop()
        {
            _calcBegin = false;

            _serverSokcet.Stop();
        }



        private void ReplyPong(IUserToken ut, QueueMsg data)
        {
            var qcoder = (Net.QueueCoder)ut.Coder;
            _serverSokcet.Send(ut.ID, qcoder.Pong(data.Name));
        }

        private void ReplyPublish(IUserToken ut, QueueMsg data)
        {
            _exchange.AcceptPublish(ut.ID, data);
        }

        private void ReplySubcribe(IUserToken ut, QueueMsg data)
        {
            var qcoder = (Net.QueueCoder)ut.Coder;

            _exchange.GetSubscribeData(ut.ID, new QueueMsg() { Name = data.Name, Topic = data.Topic }, qcoder);
        }

        private void ReplyUnsubscribe(IUserToken ut, QueueMsg data)
        {
            _exchange.Unsubscribe(data);
        }

        private void ReplyClose(IUserToken ut, QueueMsg data)
        {
            var qcoder = (Net.QueueCoder)ut.Coder;
            _serverSokcet.Send(ut.ID, qcoder.Close(data.Name));
            _exchange.Clear(ut.ID);
            _serverSokcet.Disconnect(ut.ID);
        }

        public void Clear(string sessionID)
        {
            _exchange.Clear(sessionID);
        }


        bool _calcBegin = false;

        /// <summary>
        /// 统计值
        /// </summary>
        /// <param name="callBack"></param>
        public void CalcInfo(Action<Tuple<long, long, long, long>, List<Tuple<string, long>>> callBack)
        {
            if (!_calcBegin)
            {
                _calcBegin = true;
                TaskHelper.LongRunning(() =>
                {
                    while (_calcBegin)
                    {
                        var ci = _exchange.GetConnectInfo();
                        var qi = _exchange.GetQueueInfo();
                        callBack?.Invoke(ci, qi);
                        ThreadHelper.Sleep(1000);
                    }
                });
            }

        }

    }
}
