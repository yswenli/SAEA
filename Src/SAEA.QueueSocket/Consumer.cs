/****************************************************************************
*Copyright (c) 2023 RiverLand All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WALLE
*公司名称：河之洲
*命名空间：SAEA.QueueSocket
*文件名： Consumer
*版本号： V1.0.0.0
*唯一标识：30c4bee5-d388-4ab8-9b55-6c28d2541142
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：walle.wen@tjingcai.com
*创建时间：2023/10/23 17:00:36
*描述：消费者
*
*=================================================
*修改标记
*修改时间：2023/10/23 17:00:36
*修改人： yswenli
*版本号： V1.0.0.0
*描述：消费者
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

using SAEA.QueueSocket.Model;
using SAEA.Sockets.Handler;

namespace SAEA.QueueSocket
{
    /// <summary>
    /// 消费者
    /// </summary>
    public class Consumer : IDisposable
    {

        public event OnErrorHandler OnError;

        public event OnDisconnectedHandler OnDisconnected;

        public event Action<QueueResult> OnMessage;


        QClient _consumer;

        /// <summary>
        /// 队列主题
        /// </summary>
        public string Topic { get; private set; }

        public bool Connected
        {
            get
            {
                return _consumer.Connected;
            }
        }

        /// <summary>
        /// 消费者
        /// </summary>
        /// <param name="name"></param>
        /// <param name="serverAddress"></param>
        public Consumer(string name, string serverAddress)
        {
            _consumer = new QClient(name, serverAddress);
            _consumer.OnMessage += _consumer_OnMessage;
            _consumer.OnDisconnected += _consumer_OnDisconnected;
            _consumer.OnError += _consumer_OnError;
        }


        private void _consumer_OnMessage(QueueResult obj)
        {
            if (obj != null)
            {
                OnMessage?.Invoke(obj);
            }
        }

        private void _consumer_OnDisconnected(string id, Exception ex)
        {
            OnDisconnected?.Invoke(id, ex);
        }
        private void _consumer_OnError(string id, Exception ex)
        {
            OnError?.Invoke(id, ex);
        }

        /// <summary>
        /// 订阅队列主题
        /// </summary>
        /// <param name="topic"></param>
        public void Subscribe(string topic)
        {
            Topic = topic;
        }

        /// <summary>
        /// 启动
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void Start()
        {
            if (string.IsNullOrEmpty(Topic)) throw new Exception("消费者启动前请先订阅队列主题");
            _consumer.Connect();
            _consumer.Subscribe(Topic);
        }

        /// <summary>
        /// 停止
        /// </summary>
        public void Stop()
        {
            if (_consumer.Connected)
            {
                _consumer.Unsubscribe(Topic);
                _consumer.Disconnect();
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
