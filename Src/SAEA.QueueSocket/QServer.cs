/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.QueueSocket
*文件名： QServer
*版本号： V3.2.1.1
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
*版本号： V3.2.1.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.QueueSocket.Model;
using SAEA.QueueSocket.Net;
using SAEA.QueueSocket.Type;
using SAEA.Sockets.Core;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.QueueSocket
{
    public class QServer : BaseServerSocket
    {
        Exchange _exchange;

        int _maxNum = 500;

        int _maxTime = 500;

        public QServer(int heartSpan = 20 * 1000, int bufferSize = 1000 * 1024, int count = 1000, int maxNum = 500, int maxTime = 500)
            : base(new QContext(), bufferSize, count, false)
        {
            _maxNum = maxNum;
            _maxTime = maxTime;
            _exchange = new Exchange();
        }


        protected override void OnReceiveBytes(IUserToken userToken, byte[] data)
        {
            var qcoder = (QUnpacker)userToken.Unpacker;

            qcoder.GetQueueResult(data, r =>
            {
                switch (r.Type)
                {
                    case QueueSocketMsgType.Ping:
                        ReplyPong(userToken, r);
                        break;
                    case QueueSocketMsgType.Publish:
                        ReplyPublish(userToken, r);
                        break;
                    case QueueSocketMsgType.Subcribe:
                        ReplySubcribe(userToken, r);
                        break;
                    case QueueSocketMsgType.Unsubcribe:
                        ReplyUnsubscribe(userToken, r);
                        break;
                    case QueueSocketMsgType.Close:
                        ReplyClose(userToken, r);
                        break;
                }
            });
        }

        private void ReplyPong(IUserToken ut, QueueResult data)
        {
            var qcoder = (QUnpacker)ut.Unpacker;
            base.BeginSend(ut, qcoder.QueueCoder.Pong(data.Name));
        }

        private void ReplyPublish(IUserToken ut, QueueResult data)
        {
            _exchange.AcceptPublish(ut.ID, data);
        }

        private void ReplySubcribe(IUserToken ut, QueueResult data)
        {
            Task.Factory.StartNew(() =>
            {
                var qcoder = (QUnpacker)ut.Unpacker;
                _exchange.GetSubscribeData(ut.ID, new QueueResult() { Name = data.Name, Topic = data.Topic }, _maxNum, _maxTime, (rlist) =>
                {
                    if (rlist != null)
                    {
                        var list = new List<byte>();
                        rlist.ForEach(r =>
                        {
                            list.AddRange(qcoder.QueueCoder.Data(data.Name, data.Topic, r));
                        });
                        base.BeginSend(ut, list.ToArray());
                    }
                });
            });
        }

        private void ReplyUnsubscribe(IUserToken ut, QueueResult data)
        {
            _exchange.Unsubscribe(data);
        }

        private void ReplyClose(IUserToken ut, QueueResult data)
        {
            var qcoder = (QUnpacker)ut.Unpacker;
            base.BeginSend(ut, qcoder.QueueCoder.Close(data.Name));
            _exchange.Clear(ut.ID);
            base.Disconnect(ut);
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
