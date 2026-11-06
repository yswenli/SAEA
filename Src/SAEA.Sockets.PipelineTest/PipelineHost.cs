/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Sockets.PipelineTest
*文件名： PipelineHost
*版本号： v26.11.5.1
*唯一标识：37cfb8bd-8cb6-46b9-8b01-d3f6f7fb9bb4
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：连接登记表：连接建立时写入，收口时由执行器移除
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：连接登记表：连接建立时写入，收口时由执行器移除
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.PipelineTest
{
    /// <summary>
    /// 连接登记表：连接建立时写入，收口时由执行器移除。
    /// 线程安全，同一连接标识只保留最新登记。
    /// </summary>
    public sealed class PipelineHost : IPipelineHost
    {
        private readonly ConcurrentDictionary<string, IChannel> _channels =
            new ConcurrentDictionary<string, IChannel>(StringComparer.Ordinal);

        /// <summary>当前登记数，仅供演示诊断。</summary>
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
        /// 解除登记并释放通道持有的属性。通道在此期间已被执行器标记为非活跃，
        /// 这里只做内存清理，不重复触发关闭委托。
        /// </summary>
        /// <param name="channelId">连接标识，为 null 直接返回 false</param>
        /// <param name="channel">成功时输出已移除的通道，失败时为 null</param>
        /// <returns>移除成功返回 true；标识为 null 或未登记返回 false</returns>
        public bool TryRemoveChannel(string channelId, out IChannel channel)
        {
            channel = null;
            if (channelId == null || !_channels.TryRemove(channelId, out channel))
                return false;

            var disposable = channel as IDisposable;
            if (disposable != null)
                disposable.Dispose();

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
    }
}