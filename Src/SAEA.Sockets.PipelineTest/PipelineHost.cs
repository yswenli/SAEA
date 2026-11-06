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
        public void Set(IChannel channel)
        {
            if (channel == null)
                throw new ArgumentNullException(nameof(channel));

            _channels[channel.Id] = channel;
        }

        /// <summary>按标识取通道；不存在返回 null。</summary>
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