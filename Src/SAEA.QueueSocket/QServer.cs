/****************************************************************************
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
using SAEA.Sockets.Model;

namespace SAEA.QueueSocket
{
    /// <summary>
    /// 队列服务器类
    /// </summary>
    public class QServer
    {
        Exchange _exchange;

        IServerSocket _serverSokcet;

        /// <summary>
        /// 断开连接事件
        /// </summary>
        public event OnDisconnectedHandler OnDisconnected;

        /// <summary>
        /// 初始化QServer类的新实例
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="ip">IP地址</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="maxConnects">最大连接数</param>
        /// <param name="maxPendingMsgCount">消息队列最大堆积数量，默认10000000</param>
        public QServer(int port = 39654, string ip = "127.0.0.1", int bufferSize = 128 * 1024, int maxConnects = 100, int maxPendingMsgCount = 10000000)
        {
            _exchange = new Exchange(maxPendingMsgCount);

            _exchange.OnBatched += _exchange_OnBatched;

            var config = SocketOptionBuilder.Instance
                .UseIocp<QueueCoder>()
                .SetSocket(SAEASocketType.Tcp)
                .SetReadBufferSize(bufferSize)
                .SetWriteBufferSize(bufferSize)
                .SetMaxConnects(maxConnects)
                .SetIP(ip)
                .SetPort(port)
                .Build();

            _serverSokcet = SocketFactory.CreateServerSocket(config);

            _serverSokcet.OnReceive += _serverSokcet_OnReceive;

            _serverSokcet.OnDisconnected += _serverSokcet_OnDisconnected;
        }

        /// <summary>
        /// 批量处理事件
        /// </summary>
        /// <param name="id">会话ID</param>
        /// <param name="data">数据</param>
        private void _exchange_OnBatched(string id, byte[] data)
        {
            // 使用异步发送，避免阻塞线程
            _serverSokcet.SendAsync(id, data);
        }

        /// <summary>
        /// 断开连接事件处理
        /// </summary>
        /// <param name="ID">会话ID</param>
        /// <param name="ex">异常信息</param>
        private void _serverSokcet_OnDisconnected(string ID, Exception ex)
        {
            OnDisconnected?.Invoke(ID, ex);
        }

        /// <summary>
        /// 接收数据事件处理
        /// </summary>
        /// <param name="ut">会话对象</param>
        /// <param name="data">数据</param>
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

        /// <summary>
        /// 回复消息
        /// </summary>
        /// <param name="userToken">会话对象</param>
        /// <param name="queueResult">队列消息</param>
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

        /// <summary>
        /// 启动服务器
        /// </summary>
        /// <param name="backlog">挂起连接队列的最大长度</param>
        public void Start(int backlog = 10 * 1000)
        {
            _serverSokcet.Start(backlog);

            _calcBegin = true;
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public void Stop()
        {
            _calcBegin = false;

            _serverSokcet.Stop();
        }

        /// <summary>
        /// 回复Pong消息
        /// </summary>
        /// <param name="ut">会话对象</param>
        /// <param name="data">队列消息</param>
        private void ReplyPong(IUserToken ut, QueueMsg data)
        {
            var qcoder = (Net.QueueCoder)ut.Coder;
            _serverSokcet.Send(ut.ID, qcoder.Pong(data.Name));
        }

        /// <summary>
        /// 回复发布消息
        /// </summary>
        /// <param name="ut">会话对象</param>
        /// <param name="data">队列消息</param>
        private void ReplyPublish(IUserToken ut, QueueMsg data)
        {
            _exchange.AcceptPublish(ut.ID, data);
        }

        /// <summary>
        /// 回复订阅消息
        /// </summary>
        /// <param name="ut">会话对象</param>
        /// <param name="data">队列消息</param>
        private void ReplySubcribe(IUserToken ut, QueueMsg data)
        {
            var qcoder = (Net.QueueCoder)ut.Coder;

            _exchange.GetSubscribeData(ut.ID, new QueueMsg() { Name = data.Name, Topic = data.Topic }, qcoder);
        }

        /// <summary>
        /// 回复取消订阅消息
        /// </summary>
        /// <param name="ut">会话对象</param>
        /// <param name="data">队列消息</param>
        private void ReplyUnsubscribe(IUserToken ut, QueueMsg data)
        {
            _exchange.Unsubscribe(data);
        }

        /// <summary>
        /// 回复关闭消息
        /// </summary>
        /// <param name="ut">会话对象</param>
        /// <param name="data">队列消息</param>
        private void ReplyClose(IUserToken ut, QueueMsg data)
        {
            var qcoder = (Net.QueueCoder)ut.Coder;
            _serverSokcet.Send(ut.ID, qcoder.Close(data.Name));
            _exchange.Clear(ut.ID);
            _serverSokcet.Disconnect(ut.ID);
        }

        /// <summary>
        /// 清除会话
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        public void Clear(string sessionID)
        {
            _exchange.Clear(sessionID);
        }

        bool _calcBegin = false;

        /// <summary>
        /// 统计信息
        /// </summary>
        /// <param name="callBack">回调函数</param>
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
