using System;
using System.IO.Pipelines;
using System.Threading.Tasks;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.PipelineTest
{
    /// <summary>
    /// 把 <see cref="IServerSocket"/> 接到 <see cref="IRawByteStream"/> 的适配器。
    /// <para>
    /// <see cref="SendAsync"/> 只表示"所有权已交给传输层"：<paramref name="owner"/> 必须原样
    /// 传给 <see cref="IServerSocket.SendAsync(string, ReadOnlyMemory{byte}, IDisposable)"/>，
    /// 由发送完成回调或失败路径释放，适配器绝不自行释放。
    /// </para>
    /// <para>
    /// 传输层抛出的异常统一翻译成 <see cref="ChannelClosedException"/>，让业务只面对一种关闭形态。
    /// </para>
    /// </summary>
    public sealed class SocketStreamAdapter : IRawByteStream
    {
        private readonly string _channelId;
        private readonly PipeReader _input;
        private readonly IReceiveFlowControl _flow;
        private readonly IServerSocket _server;

        /// <summary>以连接标识、PipeReader、流量控制和服务器实例创建适配器。</summary>
        public SocketStreamAdapter(
            string channelId,
            PipeReader input,
            IReceiveFlowControl flow,
            IServerSocket server)
        {
            if (string.IsNullOrEmpty(channelId))
                throw new ArgumentException("连接标识不能为空。", nameof(channelId));
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (flow == null)
                throw new ArgumentNullException(nameof(flow));
            if (server == null)
                throw new ArgumentNullException(nameof(server));

            _channelId = channelId;
            _input = input;
            _flow = flow;
            _server = server;
        }

        /// <inheritdoc />
        public string ChannelId
        {
            get { return _channelId; }
        }

        /// <inheritdoc />
        public PipeReader Input
        {
            get { return _input; }
        }

        /// <summary>把完整帧交给传输层；连接已关闭时立即抛 <see cref="ChannelClosedException"/>。</summary>
        public Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
        {
            try
            {
                _server.SendAsync(_channelId, data, owner);
                return Task.CompletedTask;
            }
            catch (ChannelClosedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ChannelClosedException("连接已关闭或发送失败:" + _channelId + "," + ex.Message, ex);
            }
        }

        /// <summary>同步暂停读取，交给传输层的应用级闸门。</summary>
        public void Pause()
        {
            _flow.Pause();
        }

        /// <summary>同步恢复读取，交给传输层的应用级闸门。</summary>
        public void Resume()
        {
            _flow.Resume();
        }

        /// <summary>关闭底层连接；传输层保证幂等。</summary>
        public Task CloseAsync(string reason)
        {
            return _flow.CloseAsync(reason);
        }
    }
}