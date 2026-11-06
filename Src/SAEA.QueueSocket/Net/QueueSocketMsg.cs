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
*命名空间：SAEA.QueueSocket.Net
*文件名： QueueSocketMsg
*版本号： v26.4.23.1
*唯一标识：ab454dce-1aca-47c7-b1d8-87e6bac6428e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：QueueSocketMsg类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:31
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;

using SAEA.Common.Caching;
using SAEA.QueueSocket.Type;

namespace SAEA.QueueSocket.Net
{
    /// <summary>
    /// 队列消息实体
    /// </summary>
    public class QueueSocketMsg : IDisposable
    {
        /// <summary>
        /// 消息最小长度：类型 1 字节 + 总长 4 字节 + 名称长度 4 字节 + 主题长度 4 字节
        /// </summary>
        public const int MINLENGTH = (1 + 4 + 4 + 0 + 4 + 0 + 0);

        /// <summary>
        /// 消息总长度，不含类型字节
        /// </summary>
        public int Total
        {
            get; set;
        }

        /// <summary>
        /// 消息类型
        /// </summary>
        public QueueSocketMsgType Type
        {
            get; set;
        }

        /// <summary>
        /// 名称字节长度
        /// </summary>
        public int NameLength
        {
            get; set;
        }

        /// <summary>
        /// 消息发送方名称
        /// </summary>
        public string Name
        {
            get; set;
        }

        /// <summary>
        /// 主题字节长度
        /// </summary>
        public int TopicLength
        {
            get; set;
        }

        /// <summary>
        /// 订阅主题
        /// </summary>
        public string Topic
        {
            get; set;
        }

        /// <summary>
        /// 消息数据，仅在持有 owner 缓冲期间有效
        /// </summary>
        public ReadOnlyMemory<byte> Data
        {
            get; set;
        }

        PooledBuffer _owner;

        /// <summary>
        /// 绑定消息数据所在的池化缓冲区，生命周期随之转移
        /// </summary>
        /// <param name="owner">池化缓冲区，为 null 时不改变数据</param>
        public void SetOwner(PooledBuffer owner)
        {
            _owner = owner;
            if (owner != null) Data = owner.AsMemory();
        }

        /// <summary>
        /// 摘除对池化缓冲区的持有，返回后由调用方负责归还
        /// </summary>
        /// <returns>此前持有的池化缓冲区，未持有时为 null</returns>
        public PooledBuffer DetachOwner()
        {
            var owner = _owner;
            _owner = null;
            return owner;
        }

        /// <summary>
        /// 以指定类型创建消息
        /// </summary>
        /// <param name="type">消息类型</param>
        public QueueSocketMsg(QueueSocketMsgType type) : this(type, null)
        {

        }

        /// <summary>
        /// 以指定类型和名称创建消息
        /// </summary>
        /// <param name="type">消息类型</param>
        /// <param name="name">消息发送方名称</param>
        public QueueSocketMsg(QueueSocketMsgType type, string name) : this(type, name, null)
        {

        }

        /// <summary>
        /// 以指定类型、名称和主题创建消息
        /// </summary>
        /// <param name="type">消息类型</param>
        /// <param name="name">消息发送方名称</param>
        /// <param name="topic">订阅主题</param>
        public QueueSocketMsg(QueueSocketMsgType type, string name, string topic) : this(type, name, topic, null)
        {

        }

        /// <summary>
        /// 以指定类型、名称、主题和数据创建消息
        /// </summary>
        /// <param name="type">消息类型</param>
        /// <param name="name">消息发送方名称</param>
        /// <param name="topic">订阅主题</param>
        /// <param name="data">消息数据，为 null 时数据为空</param>
        public QueueSocketMsg(QueueSocketMsgType type, string name, string topic, byte[] data)
        {
            this.Type = type; // 设置消息类型
            this.Name = name; // 设置消息名称
            this.Topic = topic; // 设置消息主题
            this.Data = data; // 设置消息数据
        }

        /// <summary>
        /// 归还持有的池化缓冲区并清空长度与类型
        /// </summary>
        public void Dispose()
        {
            var owner = _owner;
            _owner = null;
            Data = ReadOnlyMemory<byte>.Empty;
            if (owner != null) owner.Dispose();
            this.Total = this.NameLength = this.TopicLength = 0;
            this.Type = 0;
        }
    }
}