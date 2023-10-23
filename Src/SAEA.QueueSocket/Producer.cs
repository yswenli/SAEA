/****************************************************************************
*Copyright (c) 2023 RiverLand All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WALLE
*公司名称：河之洲
*命名空间：SAEA.QueueSocket
*文件名： Producer
*版本号： V1.0.0.0
*唯一标识：333a6705-db72-4d5a-a382-d70a201f8760
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：walle.wen@tjingcai.com
*创建时间：2023/10/23 16:53:56
*描述：生产者
*
*=================================================
*修改标记
*修改时间：2023/10/23 16:53:56
*修改人： yswenli
*版本号： V1.0.0.0
*描述：生产者
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
            _producer.Connect();
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
