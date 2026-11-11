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
*命名空间：SAEA.MessageSocket
*文件名： PipelineHostRegistry
*版本号： v26.11.5.1
*唯一标识：16821ed2-a8a5-4b90-9659-12ae7ab62eaa
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 10:00:00
*描述：消息连接的登记表
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 10:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增，适配处理器链的连接登记
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using SAEA.Sockets.Pipeline;

namespace SAEA.MessageSocket
{
    /// <summary>
    /// 连接登记表：连接建立时写入，收口时由执行器移除。
    /// 线程安全，同一连接标识只保留最新登记。
    /// </summary>
    public sealed class PipelineHostRegistry : IPipelineHost
    {
        private readonly ConcurrentDictionary<string, IChannel> _channels =
            new ConcurrentDictionary<string, IChannel>(StringComparer.Ordinal);

        /// <summary>当前登记数，仅供诊断。</summary>
        public int Count
        {
            get { return _channels.Count; }
        }

        /// <summary>登记一条连接；同一标识覆盖旧值。</summary>
        /// <param name="channel">待登记的通道，为 null 抛 ArgumentNullException</param>
        public void Set(IChannel channel)
        {
            if (channel == null)
                throw new ArgumentNullException(nameof(channel));

            _channels[channel.Id] = channel;
        }

        /// <summary>按标识取通道；不存在返回 null。</summary>
        /// <param name="channelId">连接标识，为 null 返回 null</param>
        /// <returns>已登记的通道，未登记时为 null</returns>
        public IChannel Get(string channelId)
        {
            IChannel channel;
            return channelId != null && _channels.TryGetValue(channelId, out channel) ? channel : null;
        }

        /// <inheritdoc />
        public bool TryGetChannel(string channelId, out IChannel channel)
        {
            channel = null;
            return channelId != null && _channels.TryGetValue(channelId, out channel);
        }

        /// <summary>
        /// 解除登记并释放该通道的连接级属性。只有真正移除成功的一次会清理属性，
        /// 重复移除返回 false 且不再触碰通道。
        /// </summary>
        /// <param name="channelId">连接标识，为 null 直接返回 false</param>
        /// <param name="channel">成功时输出已移除的通道，失败时为 null</param>
        /// <returns>移除成功返回 true；标识为 null 或未登记返回 false</returns>
        public bool TryRemoveChannel(string channelId, out IChannel channel)
        {
            channel = null;
            if (channelId == null || !_channels.TryRemove(channelId, out channel))
                return false;

            ReleaseAttributes(channel);
            return true;
        }

        /// <inheritdoc />
        public Task CloseAsync(string channelId, string reason)
        {
            IChannel channel;
            if (channelId == null || !_channels.TryGetValue(channelId, out channel))
                return Task.CompletedTask;

            return channel.CloseAsync(reason);
        }

        /// <summary>逐项移除属性；快照键后删除，避免遍历与删除并发冲突。</summary>
        /// <param name="channel">已从登记表移除的通道</param>
        private static void ReleaseAttributes(IChannel channel)
        {
            if (channel == null)
                return;

            var attributes = channel.Attributes;
            if (attributes == null || attributes.Count == 0)
                return;

            var keys = new List<string>(attributes.Count);
            foreach (var pair in attributes)
                keys.Add(pair.Key);

            for (var i = 0; i < keys.Count; i++)
            {
                object ignored;
                channel.TryRemoveAttribute(keys[i], out ignored);
            }
        }
    }
}