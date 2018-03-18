/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.QueueSocket
*文件名： QServer
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/

using SAEA.Commom;
using SAEA.QueueSocket.Model;
using SAEA.QueueSocket.Net;
using SAEA.QueueSocket.Type;
using SAEA.Sockets.Core;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAEA.QueueSocket
{
    public class QServer : BaseServerSocket
    {
        int _heartSpan = 20 * 1000;

        Exchange _exchange;

        int _maxNum = 500;

        int _maxTime = 500;

        public QServer(int heartSpan = 20 * 1000, int bufferSize = 100 * 1024, int count = 10000, int maxNum = 500, int maxTime = 500)
            : base(new QContext(), bufferSize, true, count)
        {
            _heartSpan = heartSpan;
            _maxNum = maxNum;
            _maxTime = maxTime;
            _exchange = new Exchange();
        }


        protected override void OnReceiveBytes(IUserToken userToken, byte[] data)
        {
            userToken.Coder.Pack(data, null, (s) =>
            {
                switch (s.Type)
                {
                    case (byte)QueueSocketMsgType.Ping:
                        ReplyPong(userToken, s.Content);
                        break;
                    case (byte)QueueSocketMsgType.Pong:
                        ReplyPing(userToken, s.Content);
                        break;
                    case (byte)QueueSocketMsgType.Publish:
                        ReplyPublish(userToken, s.Content);
                        break;
                    case (byte)QueueSocketMsgType.PublishForBatch:
                        ReplyPublishForBatch(userToken, s.Content);
                        break;
                    case (byte)QueueSocketMsgType.Subcribe:
                        ReplySubcribe(userToken, s.Content);
                        break;
                    case (byte)QueueSocketMsgType.Unsubcribe:
                        ReplyUnsubscribe(userToken, s.Content);
                        break;
                    case (byte)QueueSocketMsgType.Close:
                        ReplyClose(userToken, s.Content);
                        break;
                }

            }, null);
        }


        private void ReplyBase(IUserToken ut, QueueSocketMsgType type, byte[] content)
        {
            var byts = QueueSocketMsg.Parse(content, type).ToBytes();
            base.Send(ut, byts);
        }

        private void ReplyPong(IUserToken ut, byte[] data)
        {
            ReplyBase(ut, QueueSocketMsgType.Pong, data);
        }
        private void ReplyPing(IUserToken ut, byte[] data)
        {
            ReplyBase(ut, QueueSocketMsgType.Ping, data);
        }

        private void ReplyPublish(IUserToken ut, byte[] data)
        {
            _exchange.AcceptPublish(ut.ID, data.ToInstance<PublishInfo>());
            ReplyBase(ut, QueueSocketMsgType.Publish, null);
        }

        private void ReplyPublishForBatch(IUserToken ut, byte[] data)
        {
            _exchange.AcceptPublishForBatch(ut.ID, data);
            ReplyBase(ut, QueueSocketMsgType.PublishForBatch, null);
        }

        private void ReplySubcribe(IUserToken ut, byte[] data)
        {
            var sdata = data.ToInstance<SubscribeInfo>();
            _exchange.GetSubscribeData(ut.ID, new SubscribeInfo() { Name = sdata.Name, Topic = sdata.Topic }, _maxNum, _maxTime, (r) =>
            {
                ReplyBase(ut, QueueSocketMsgType.Data, r.ToBytes());
            });
        }

        private void ReplyUnsubscribe(IUserToken ut, byte[] data)
        {
            var udata = data.ToInstance<SubscribeInfo>();
            _exchange.Unsubscribe(udata);
        }

        private void ReplyClose(IUserToken ut, byte[] data)
        {
            ReplyBase(ut, QueueSocketMsgType.Close, data);
            _exchange.Clear(ut.ID);
            base.Disconnected(ut);
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
                Task.Factory.StartNew(() =>
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
