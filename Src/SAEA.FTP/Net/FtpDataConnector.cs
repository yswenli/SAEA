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
*命名空间：SAEA.FTP.Net
*文件名： FtpDataConnector
*版本号： v26.11.5.1
*唯一标识：11b7a0f1-1fd1-45fa-b890-d32e205cf86f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 17:52:00
*描述：FTP 客户端数据连接连接器，负责建链、装配数据链并交出连接门面
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 17:57:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：修正 banner 空格行并对齐模板
*
*****************************************************************************/
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.FTP.Net
{
    /// <summary>
    /// 一次主动数据连接的连接器：建立客户端套接字、装配数据通道链、把连接交给传输代码。
    /// <para>
    /// 一次性语义：每个实例只服务一次 <see cref="Connect"/>，成功后连接所有权转给
    /// <see cref="FtpDataConnection"/>，本实例不再关闭它。
    /// </para>
    /// <para>
    /// 交接时机与被动监听器一致：连接必须在链启动之后才交出去，否则首块数据可能早于写入口绑定而被丢弃；
    /// 因此调用方拿到 <see cref="FtpDataConnection"/> 时链已在运行。
    /// </para>
    /// </summary>
    internal sealed class FtpDataConnector : IDisposable
    {
        private readonly Action<string, Exception> _log;

        private readonly ChannelPipelineOptions _options;

        private readonly PipelineHostRegistry _host = new PipelineHostRegistry();

        private readonly TaskCompletionSource<FtpDataConnection> _connected =
            new TaskCompletionSource<FtpDataConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

        // 连接建立时写入，之后只读；与 RunPipelineAsync 跨线程可见性由 Volatile 保证。
        private ChannelInfo _channel;

        private ChannelStreamAdapter _adapter;

        private IClientSocket _client;

        private int _disposed;

        /// <summary>
        /// 以日志回调和数据通道链配置创建连接器，创建后需调用 <see cref="Connect"/> 才会建链。
        /// </summary>
        /// <param name="log">传输层异常回调，为 null 时不记录</param>
        /// <param name="options">数据通道链配置，为 null 抛 <see cref="ArgumentNullException"/></param>
        internal FtpDataConnector(Action<string, Exception> log, ChannelPipelineOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            _log = log;
            _options = options;

            // 调用方可能已经超时或被释放，这里观察异常，避免留下未观察的任务异常。
            _connected.Task.ContinueWith(
                task => { var ignored = task.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>
        /// 连接远端数据端口并等待链启动；超时或失败时就地释放套接字与适配器。
        /// </summary>
        /// <param name="host">数据端口主机，为空或 null 抛 <see cref="ArgumentException"/></param>
        /// <param name="port">数据端口，超出 1–65535 抛 <see cref="ArgumentOutOfRangeException"/></param>
        /// <param name="timeout">连接与等待链启动的上限，小于等于 0 时按 60 秒处理</param>
        /// <param name="readTimeout">读超时（毫秒），小于 1000 时按 1000 处理</param>
        /// <param name="writeTimeout">写超时（毫秒），小于 1000 时按 1000 处理</param>
        /// <returns>已完成链装配、可以读写的数据连接门面</returns>
        internal FtpDataConnection Connect(string host, int port, TimeSpan timeout, int readTimeout, int writeTimeout)
        {
            if (string.IsNullOrEmpty(host))
                throw new ArgumentException("数据连接主机不能为空。", nameof(host));
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port));

            if (timeout <= TimeSpan.Zero)
                timeout = TimeSpan.FromSeconds(60);

            var option = new SocketOptionBuilder()
                .SetSocket(SAEASocketType.Tcp)
                .UseStream()
                .UseConnectionOpened(OnConnectionOpened)
                .UseInputHandler(RunPipelineAsync)
                .SetIP(host)
                .SetPort((ushort)port)
                .SetReadBufferSize(64 * 1024)
                .SetWriteBufferSize(64 * 1024)
                .SetConnectTimeout((int)Math.Max(1, timeout.TotalMilliseconds))
                .Build();

            var client = SocketFactory.CreateClientSocket(option);

            // 先登记再连接：连接钩子与读循环都会从这里取客户端实例。
            _client = client;

            if (_log != null)
                client.OnError += OnError;

            try
            {
                client.Connect();

                var pending = _connected.Task;

                // 用 WhenAny 判超时：pending 以异常完成时原样抛出，不会被包成 AggregateException。
                var completed = Task.WhenAny(pending, Task.Delay(timeout)).GetAwaiter().GetResult();

                if (!ReferenceEquals(completed, pending))
                {
                    // 超时判定可能与交接同一瞬间发生：已建好却没人接收的连接必须就地关闭，否则泄漏。
                    if (pending.Status == TaskStatus.RanToCompletion)
                    {
                        try { pending.GetAwaiter().GetResult().Close(); } catch (Exception) { }
                    }

                    throw new TimeoutException("FTP data connect timeout: " + host + ":" + port);
                }

                var connection = pending.GetAwaiter().GetResult();

                // 与旧 NetworkStream 行为一致：读写等待不能是 0（无限等待），至少 1 秒。
                connection.ReadTimeout = Math.Max(1000, readTimeout);
                connection.WriteTimeout = Math.Max(1000, writeTimeout);

                return connection;
            }
            catch
            {
                // 任何失败都不留挂在死管道上的资源：未交接时这里会释放适配器与套接字。
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// 释放连接器：从未交接出去时归还适配器并释放客户端实例；可重复调用。
        /// <para>
        /// 已交接出去的连接由 <see cref="FtpDataConnection.Close"/> 负责，本类不重复关闭。
        /// </para>
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            if (_connected.Task.Status == TaskStatus.RanToCompletion)
                return;

            if (_connected.TrySetException(new ObjectDisposedException(nameof(FtpDataConnector))))
            {
                // 连接从未交接出去：适配器仍归本实例所有，必须在这里归还。
                var adapter = Volatile.Read(ref _adapter);
                if (adapter != null)
                {
                    try { adapter.Dispose(); } catch (Exception) { }
                }
            }

            var client = _client;
            _client = null;

            if (client != null)
            {
                try { client.OnError -= OnError; } catch (Exception) { }
                try { client.Dispose(); } catch (Exception) { }
            }
        }

        /// <summary>
        /// 连接建立回调：登记首条连接，先写适配器再写通道。
        /// </summary>
        /// <param name="info">底层通道信息，含连接标识与套接字</param>
        private void OnConnectionOpened(ChannelInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.ID))
                return;

            // 先写适配器再写通道：读侧以「通道已登记」作为适配器可用的判定。
            Volatile.Write(ref _adapter, new ChannelStreamAdapter());
            Volatile.Write(ref _channel, info);
        }

        /// <summary>
        /// 链启动后的回调：把连接交给等待方；没人接收时就地关闭，避免泄漏。
        /// </summary>
        /// <param name="channelId">连接标识</param>
        /// <param name="pipeline">已启动的处理器链</param>
        private void OnPipelineStarted(string channelId, IChannelPipeline pipeline)
        {
            var info = Volatile.Read(ref _channel);
            var adapter = Volatile.Read(ref _adapter);
            var client = _client;

            if (info == null || adapter == null || client == null)
                return;

            var connection = FtpDataConnection.CreateForClient(client, info.ClientSocket, adapter);

            if (!_connected.TrySetResult(connection))
            {
                // 等待方已超时或连接器已释放：没有消费者，必须在这里关闭。
                connection.Close();
            }
        }

        /// <summary>
        /// 装配数据通道链：出站前缀 → 入站解码 → 失活桥接 → 链尾异常处理器。
        /// </summary>
        /// <param name="channelId">连接标识</param>
        /// <param name="pipeline">尚未启动的处理器链</param>
        private void ConfigureChain(string channelId, IChannelPipeline pipeline)
        {
            var adapter = Volatile.Read(ref _adapter);
            if (adapter == null)
                throw new InvalidOperationException("数据通道适配器尚未创建，无法装配数据通道链。");

            // 出站必须是链首连续前缀，串行器固定在最右，否则同一连接上的多次写会乱序。
            pipeline.AddLast(new FtpDataChunkEncoder());
            pipeline.AddLast(new OutboundSerializingHandler());

            pipeline.AddLast(new FtpDataChunkDecoder(adapter));
            pipeline.AddLast(new FtpDataFaultHandler(adapter));
            pipeline.AddLast(new ExceptionTerminalHandler());

            // 启动前绑定写入口：连接交接出去后传输代码可能立刻写出首块。
            adapter.AttachPipeline(pipeline);
        }

        /// <summary>
        /// 接管数据连接的读循环：只为本实例的连接建链，运行到执行器收口。
        /// </summary>
        /// <param name="userToken">连接令牌，用于取连接标识</param>
        /// <param name="reader">接收方向的 PipeReader</param>
        /// <param name="flow">应用级流量控制闸门</param>
        /// <param name="token">连接级取消令牌</param>
        /// <returns>执行器收口完成后结束的任务</returns>
        private async Task RunPipelineAsync(
            IUserToken userToken,
            PipeReader reader,
            IReceiveFlowControl flow,
            CancellationToken token)
        {
            var channelId = userToken == null ? null : userToken.ID;

            if (string.IsNullOrEmpty(channelId))
                return;

            var info = Volatile.Read(ref _channel);
            var adapter = Volatile.Read(ref _adapter);
            var client = _client;

            // 没有适配器或标识不符时不建链，避免在错误的连接上启动执行器。
            if (info == null || adapter == null || client == null)
                return;
            if (!string.Equals(info.ID, channelId, StringComparison.Ordinal))
                return;

            var stream = new PipelineRawByteStream(
                channelId,
                reader,
                flow,
                (data, owner) => client.SendAsync(data, owner));

            try
            {
                await PipelineWiring.RunAsync(
                    channelId,
                    stream,
                    flow,
                    _options,
                    token,
                    _host,
                    ConfigureChain,
                    OnPipelineStarted)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 没能交接出去时由本方法归还适配器；已交接的由 FtpDataConnection.Close 负责。
                if (_connected.TrySetException(ex))
                    adapter.Dispose();
            }
        }

        /// <summary>
        /// 底层传输层异常上报：交给构造时传入的日志回调，回调自身异常不外抛。
        /// </summary>
        /// <param name="id">连接标识</param>
        /// <param name="ex">异常</param>
        private void OnError(string id, Exception ex)
        {
            var log = _log;
            if (log == null)
                return;

            try { log(id, ex); } catch (Exception) { }
        }
    }
}