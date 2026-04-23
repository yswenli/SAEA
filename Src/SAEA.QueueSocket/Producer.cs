/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.QueueSocket
*文件名： Producer
*版本号： v26.4.23.1
*唯一标识：0e498315-28ed-432d-a0d9-052036129ee6
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2023/10/23 17:38:33
*描述：Producer生产者类
*
*=====================================================================
*修改标记
*修改时间：2023/10/23 17:38:33
*修改人： yswenli
*版本号： v26.4.23.1
*描述：Producer生产者类
*
*****************************************************************************/
using System;

using SAEA.Sockets.Handler;

namespace SAEA.QueueSocket
{
    /// <summary>
    /// 生产者
    /// </summary>
    public class Producer : IDisposable
    {

        public event OnErrorHandler OnError;

        public event OnDisconnectedHandler OnDisconnected;

        /// <summary>
        /// 消息实际发送完成事件
        /// </summary>
        public event Action<int> OnMessagesSent;

        QClient _producer;

        public bool Connected
        {
            get
            {
                return _producer.Connected;
            }
        }

        /// <summary>
        /// 生产者
        /// </summary>
        /// <param name="name"></param>
        /// <param name="serverAddress"></param>
        public Producer(string name, string serverAddress)
        {
            _producer = new QClient(name, serverAddress);
            _producer.OnError += _consumer_OnError;
            _producer.OnDisconnected += _consumer_OnDisconnected;
            _producer.OnMessagesSent += _producer_OnMessagesSent;
            _producer.Connect();
        }


        private void _consumer_OnDisconnected(string id, Exception ex)
        {
            if (string.IsNullOrEmpty(id)) return;
            OnDisconnected?.Invoke(id, ex);
        }
        private void _consumer_OnError(string id, Exception ex)
        {
            if (string.IsNullOrEmpty(id)) return;
            OnError?.Invoke(id, ex);
        }

        private void _producer_OnMessagesSent(int count)
        {
            OnMessagesSent?.Invoke(count);
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="content"></param>
        public void Publish(string topic, string content)
        {
            _producer.Publish(topic, content);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            _producer.Close();
        }
    }
}