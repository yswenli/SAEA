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
*文件名： Consumer
*版本号： v26.4.23.1
*唯一标识：1de15eb9-a067-4d64-88b6-ddf478885902
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2023/10/23 17:38:33
*描述：Consumer消费者类
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 20:10:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：删除接收与生命周期事件，改为透传处理器链装配回调，移除编码器缓冲区清理任务
*
*****************************************************************************/
using System;

using SAEA.Sockets.Pipeline;

namespace SAEA.QueueSocket
{
    /// <summary>
    /// 消费者
    /// </summary>
    public class Consumer : IDisposable
    {

        QClient _consumer;

        /// <summary>
        /// 队列主题
        /// </summary>
        public string Topic { get; private set; }

        /// <summary>
        /// 当前是否已连接服务端
        /// </summary>
        public bool Connected
        {
            get
            {
                return _consumer.Connected;
            }
        }

        /// <summary>
        /// 处理器链装配回调：收到订阅主题的消息时由使用者处理器处理，
        /// 参数为连接标识与尚未启动的处理器链。
        /// </summary>
        public Action<string, IChannelPipeline> ConfigurePipeline
        {
            get
            {
                return _consumer.ConfigurePipeline;
            }
            set
            {
                _consumer.ConfigurePipeline = value;
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

        /// <summary>
        /// 断开连接并释放底层客户端
        /// </summary>
        public void Dispose()
        {
            Stop();
            _consumer?.Dispose();
        }
    }
}