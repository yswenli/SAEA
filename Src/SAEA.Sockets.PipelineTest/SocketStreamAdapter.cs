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
*文件名： SocketStreamAdapter
*版本号： v26.11.5.1
*唯一标识：dfdb38ee-4130-455c-99dc-a240cff91296
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：把服务端或客户端 socket 接到 IRawByteStream 的适配器
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 14:12:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：写明发送 Task 只表示所有权交接，不表示对端收到
*
*****************************************************************************/
using System;
using System.IO.Pipelines;
using System.Threading.Tasks;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.PipelineTest
{
    /// <summary>
    /// 把服务端或客户端 socket 接到 <see cref="IRawByteStream"/> 的适配器。
    /// <para>
    /// <see cref="SendAsync"/> 把<paramref name="owner"/>原样交给发送委托或
    /// <see cref="IServerSocket.SendAsync(string, ReadOnlyMemory{byte}, IDisposable)"/>，
    /// 由传输层在发送完成或失败路径释放，适配器绝不自行释放。
    /// </para>
    /// <para>
    /// 返回的 Task 只表示「所有权已交接给传输层」，不表示对端已经收到：
    /// 内核缓冲区之后的路由、重传与对端读取都不在等待范围内。
    /// </para>
    /// <para>
    /// 同步抛出的异常与异步 fault 统一翻译成 <see cref="ChannelClosedException"/>，让业务只面对一种关闭形态。
    /// </para>
    /// </summary>
    public sealed class SocketStreamAdapter : IRawByteStream
    {
        private readonly string _channelId;
        private readonly PipeReader _input;
        private readonly IReceiveFlowControl _flow;
        private readonly Func<ReadOnlyMemory<byte>, IDisposable, Task> _send;

        /// <summary>以连接标识、PipeReader、流量控制和服务器实例创建适配器。</summary>
        /// <param name="channelId">连接标识，为空或 null 抛 ArgumentException</param>
        /// <param name="input">接收方向的 PipeReader，为 null 抛 ArgumentNullException</param>
        /// <param name="flow">应用级流量控制闸门，为 null 抛 ArgumentNullException</param>
        /// <param name="server">底层传输层服务器，为 null 抛 ArgumentNullException</param>
        public SocketStreamAdapter(
            string channelId,
            PipeReader input,
            IReceiveFlowControl flow,
            IServerSocket server)
            : this(channelId, input, flow, CreateServerSend(channelId, server))
        {
        }

        /// <summary>以连接标识、PipeReader、流量控制和发送委托创建适配器，客户端接入时使用。</summary>
        /// <param name="channelId">连接标识，为空或 null 抛 ArgumentException</param>
        /// <param name="input">接收方向的 PipeReader，为 null 抛 ArgumentNullException</param>
        /// <param name="flow">应用级流量控制闸门，为 null 抛 ArgumentNullException</param>
        /// <param name="send">发送委托，接收完整帧与所有权凭据，为 null 抛 ArgumentNullException</param>
        public SocketStreamAdapter(
            string channelId,
            PipeReader input,
            IReceiveFlowControl flow,
            Func<ReadOnlyMemory<byte>, IDisposable, Task> send)
        {
            if (string.IsNullOrEmpty(channelId))
                throw new ArgumentException("连接标识不能为空。", nameof(channelId));
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (flow == null)
                throw new ArgumentNullException(nameof(flow));
            if (send == null)
                throw new ArgumentNullException(nameof(send));

            _channelId = channelId;
            _input = input;
            _flow = flow;
            _send = send;
        }

        private static Func<ReadOnlyMemory<byte>, IDisposable, Task> CreateServerSend(
            string channelId,
            IServerSocket server)
        {
            if (server == null)
                throw new ArgumentNullException(nameof(server));

            return (data, owner) => server.SendAsync(channelId, data, owner);
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

        /// <summary>把完整帧交给传输层，并等待所有权交接完成；失败时以 <see cref="ChannelClosedException"/> 结束。</summary>
        /// <param name="data">待发送的完整帧</param>
        /// <param name="owner">帧缓冲区所有权凭据，由传输层在发送完成或失败后释放，适配器不释放</param>
        /// <returns>
        /// 所有权交接完成后完成的 Task；只表示已交给传输层，不表示对端收到。
        /// 同步异常与异步 fault 都会翻译成 <see cref="ChannelClosedException"/>。
        /// </returns>
        public async Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
        {
            Task sending;
            try
            {
                sending = _send(data, owner);
            }
            catch (ChannelClosedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ChannelClosedException("连接已关闭或发送失败:" + _channelId + "," + ex.Message, ex);
            }

            if (sending == null) return;

            try
            {
                await sending.ConfigureAwait(false);
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
        /// <param name="reason">关闭原因，透传给流量控制层</param>
        /// <returns>关闭完成的 Task，可重复调用</returns>
        public Task CloseAsync(string reason)
        {
            return _flow.CloseAsync(reason);
        }
    }
}