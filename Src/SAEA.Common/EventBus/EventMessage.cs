/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Common.EventBus
*文件名： EventMessage
*版本号： v26.11.5.1
*唯一标识：d4f0ef14-a268-404d-a038-51b8a7c5666b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 12:40:36
*描述：事件消息
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:45
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;

namespace SAEA.Common.EventBus
{
    /// <summary>
    /// 事件总线消息
    /// </summary>
    public class EventMessage
    {
        /// <summary>
        /// 主题
        /// </summary>
        public string Topic { get; }
        /// <summary>
        /// 消息数据
        /// </summary>
        public byte[] Data { get; }
        /// <summary>
        /// 附加上下文对象，可为 null
        /// </summary>
        public object Context { get; }
        /// <summary>
        /// 创建时间，UTC 时间
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 创建事件消息，时间戳取构造时的 UTC 时间
        /// </summary>
        /// <param name="topic">主题，不能为 null</param>
        /// <param name="data">消息数据，不能为 null</param>
        /// <param name="context">附加上下文对象，可为 null</param>
        public EventMessage(string topic, byte[] data, object context = null)
        {
            Topic = topic ?? throw new ArgumentNullException(nameof(topic));
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Context = context;
            Timestamp = DateTime.UtcNow;
        }
    }
}