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
*修改时间：2026/11/11 11:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：装配回调改为启动前注入：Start/Connect 之后再赋值抛 InvalidOperationException，中英 README 同步说明
*
*****************************************************************************/
using System;

using SAEA.Sockets.Pipeline;

namespace SAEA.QueueSocket
{
    /// <summary>
    /// 生产者
    /// </summary>
    public class Producer : IDisposable
    {

        QClient _producer;

        /// <summary>
        /// 当前是否已连接服务端
        /// </summary>
        public bool Connected
        {
            get
            {
                return _producer.Connected;
            }
        }

        /// <summary>
        /// 处理器链装配回调：参数为连接标识与尚未启动的处理器链。
        /// 发布不需要入站处理器，为 null 时只保留内置处理器。
        /// 构造函数内即建立连接，因此只能在创建实例时通过构造参数注入，之后再赋值会抛
        /// InvalidOperationException。
        /// </summary>
        public Action<string, IChannelPipeline> ConfigurePipeline
        {
            get
            {
                return _producer.ConfigurePipeline;
            }
            set
            {
                _producer.ConfigurePipeline = value;
            }
        }

        /// <summary>
        /// 生产者
        /// </summary>
        /// <param name="name"></param>
        /// <param name="serverAddress"></param>
        public Producer(string name, string serverAddress) : this(name, serverAddress, null)
        {
        }

        /// <summary>
        /// 生产者，并在建立连接前装配处理器链
        /// </summary>
        /// <param name="name">生产者名称</param>
        /// <param name="serverAddress">服务端地址</param>
        /// <param name="configurePipeline">处理器链装配回调，连接建立前写下，建链时生效</param>
        public Producer(string name, string serverAddress, Action<string, IChannelPipeline> configurePipeline)
        {
            _producer = new QClient(name, serverAddress);

            if (configurePipeline != null)
                _producer.ConfigurePipeline = configurePipeline;

            _producer.Connect();
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
        /// 关闭底层客户端连接
        /// </summary>
        public void Dispose()
        {
            _producer.Close();
        }
    }
}