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
*文件名： FtpDataListener
*版本号： v26.11.5.1
*唯一标识：a0973891-49f1-44d5-afc0-3e5107ab936e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 18:20:00
*描述：FTP 被动模式数据监听器，每个实例只服务首条数据连接
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 18:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 FTP 被动模式数据监听器
*
*****************************************************************************/
using System;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.FTP.Net
{
    /// <summary>
    /// 一条 PASV/EPSV 命令对应的数据监听器：接受首条连接、装配数据通道链、把连接交给传输代码。
    /// <para>
    /// 一次性语义：每个实例只服务首条连接，多余连接接受后立即断开，传输结束后由会话释放整个监听器。
    /// 实例各自持有连接登记表，避免不同数据监听器之间按会话标识互相覆盖。
    /// </para>
    /// <para>
    /// 交接时机：连接必须在链启动之后才交给传输代码，否则首块数据可能早于写入口绑定而被丢弃；
    /// 因此等待方拿到 <see cref="FtpDataConnection"/> 时链已在运行。
    /// </para>
    /// </summary>
    internal sealed class FtpDataListener : IDisposable
    {
        private readonly FTPServer _ftpServer;

        private readonly ChannelPipelineOptions _options;

        private readonly PipelineHostRegistry _host = new PipelineHostRegistry();

        private readonly TaskCompletionSource<FtpDataConnection> _accepted =
            new TaskCompletionSource<FtpDataConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

        // 首条连接建立时写入，之后只读；与 RunPipelineAsync 跨线程可见性由 Volatile 保证。
        private ChannelInfo _channel;

        private ChannelStreamAdapter _adapter;

        private SaeaServerSocket _server;

        private int _claimed;

        private int _disposed;

        /// <summary>
        /// 以所属服务端与数据通道链配置创建监听器，创建后需调用 <see cref="TryStart"/> 才会监听。
        /// </summary>
        /// <param name="ftpServer">所属服务端，用于取端口范围与日志，为 null 抛 <see cref="ArgumentNullException"/></param>
        /// <param name="options">数据通道链配置，为 null 抛 <see cref="ArgumentNullException"/></param>
        internal FtpDataListener(FTPServer ftpServer, ChannelPipelineOptions options)
        {
            if (ftpServer == null)
                throw new ArgumentNullException(nameof(ftpServer));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            _ftpServer = ftpServer;
            _options = options;

            // 等待方可能已经超时或监听器先被释放，这里观察异常，避免留下未观察的任务异常。
            _accepted.Task.ContinueWith(
                task => { var ignored = task.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>
        /// 正在监听的端口；未启动或启动失败时为 0。
        /// </summary>
        internal int Port { get; private set; }

        /// <summary>
        /// 在配置的被动端口范围内逐个尝试监听，全部占用时回退系统分配；成功返回 true。
        /// </summary>
        /// <returns>是否已开始监听</returns>
        internal bool TryStart()
        {
            var config = _ftpServer.Config;

            var min = config.PassivePortMin;
            var max = config.PassivePortMax;

            if (min > 0 && max >= min)
            {
                for (int port = min; port <= max; port++)
                {
                    if (TryListen(port))
                        return true;
                }
            }

            return TryListen(0);
        }

        /// <summary>
        /// 阻塞等待首条数据连接完成链启动；超时会就地释放监听器并抛 <see cref="TimeoutException"/>。
        /// </summary>
        /// <param name="timeout">等待上限，小于等于 0 时按 60 秒处理</param>
        /// <returns>已完成链装配、可以读写的连接门面</returns>
        internal FtpDataConnection Accept(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero)
                timeout = TimeSpan.FromSeconds(60);

            var pending = _accepted.Task;

            // 用 WhenAny 判超时：pending 以异常完成时直接原样抛出，不会被包成 AggregateException。
            var completed = Task.WhenAny(pending, Task.Delay(timeout)).GetAwaiter().GetResult();

            if (!ReferenceEquals(completed, pending))
            {
                // 超时后没有人会再消费这条连接，立刻收口监听器，避免端口占用到进程退出。
                Dispose();
                throw new TimeoutException("Waiting for FTP data connection timed out.");
            }

            return pending.GetAwaiter().GetResult();
        }

        /// <summary>
        /// 释放监听器：关闭已交接的数据连接，再停止监听并清空登记。可重复调用。
        /// <para>
        /// 顺序不可颠倒：底层服务器释放会连带杀掉已接受连接，先放监听器会截断尾部数据。
        /// </para>
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            FtpDataConnection accepted = null;

            var task = _accepted.Task;
            if (task.Status == TaskStatus.RanToCompletion)
                accepted = task.Result;

            if (accepted != null)
            {
                try { accepted.Close(); } catch (Exception) { }
            }
            else if (_accepted.TrySetException(new ObjectDisposedException(nameof(FtpDataListener))))
            {
                // 连接从未交接出去：适配器仍归监听器所有，必须在这里归还。
                var adapter = Volatile.Read(ref _adapter);
                if (adapter != null)
                {
                    try { adapter.Dispose(); } catch (Exception) { }
                }
            }

            DetachServer(_server);
        }

        /// <summary>
        /// 在一个指定端口上新建并启动监听；端口不可用返回 false，其它异常记录日志后返回 false。
        /// </summary>
        /// <param name="port">监听端口，0 表示由系统分配</param>
        /// <returns>是否成功监听</returns>
        private bool TryListen(int port)
        {
            SaeaServerSocket server = null;

            try
            {
                var option = new SocketOptionBuilder()
                    .SetSocket(SAEASocketType.Tcp)
                    .UseStream()
                    .UseConnectionOpened(OnConnectionOpened)
                    .UseInputHandler(RunPipelineAsync)
                    .SetIP(string.Empty)
                    .SetPort(port)
                    .SetReadBufferSize(64 * 1024)
                    .SetWriteBufferSize(64 * 1024)
                    // 首连接之外的备用连接也会被接受，才能立刻断开它；令牌池按该上限预热，不宜放大。
                    .SetMaxConnects(2)
                    .Build();

                server = new SaeaServerSocket(option);
                server.OnError += OnError;

                // 必须先挂上再启动：接受回调可能早于 Start 返回，回调里要靠这个字段断开多余连接。
                _server = server;
                server.Start();

                var endPoint = server.LocalEndPoint;
                if (endPoint == null || endPoint.Port <= 0)
                {
                    DetachServer(server);
                    return false;
                }

                Port = endPoint.Port;
                return true;
            }
            catch (SocketException)
            {
                // 端口被占用或本机不可用属于范围内候选失败，直接换下一个端口。
                DetachServer(server);
                return false;
            }
            catch (Exception ex)
            {
                _ftpServer.Log("创建被动端口失败", ex);
                DetachServer(server);
                return false;
            }
        }

        /// <summary>
        /// 摘掉错误订阅与监听器实例；只在实例仍挂在本对象上时清空字段。
        /// </summary>
        /// <param name="server">待释放的底层服务器，可为 null</param>
        private void DetachServer(SaeaServerSocket server)
        {
            if (server == null)
                return;

            if (ReferenceEquals(_server, server))
                _server = null;

            try { server.OnError -= OnError; } catch (Exception) { }

            // 释放会一并停止监听并清理会话，失败也不足以上抛：端口已无可用路径。
            try { server.Dispose(); } catch (Exception) { }
        }

        /// <summary>
        /// 连接建立回调：登记首条连接，多余连接直接断开。
        /// </summary>
        /// <param name="info">底层通道信息，含连接标识与套接字</param>
        private void OnConnectionOpened(ChannelInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.ID))
                return;

            var server = _server;

            // 一次性认领：只有首条连接进入传输，其余连接进来就断开，不留无主空会话。
            if (Interlocked.CompareExchange(ref _claimed, 1, 0) != 0)
            {
                if (server != null)
                {
                    try { server.Disconnect(info.ID); } catch (Exception) { }
                }

                return;
            }

            if (server != null)
            {
                // 数据连接可能在长传输或对端停顿期间长时间空闲，不能按控制连接的空闲超时清理。
                try { server.SessionManager.SetNoTimeout(info.ID, true); } catch (Exception) { }
            }

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
            var server = _server;

            if (info == null || adapter == null || server == null)
                return;

            var connection = new FtpDataConnection(server, channelId, info.ClientSocket, adapter);

            if (!_accepted.TrySetResult(connection))
            {
                // 等待方已超时或监听器已释放：没有消费者，必须在这里关闭。
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
        /// 接管一条数据连接的读循环：只为首条连接建链，运行到执行器收口。
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
            var server = _server;

            // 被拒绝的备用连接也会走输入处理器：没有适配器或标识不符时直接退出，不建链。
            if (info == null || adapter == null || server == null)
                return;
            if (!string.Equals(info.ID, channelId, StringComparison.Ordinal))
                return;

            var stream = new PipelineRawByteStream(channelId, reader, flow, server);

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
                if (_accepted.TrySetException(ex))
                    adapter.Dispose();
            }
        }

        /// <summary>
        /// 底层传输层异常上报：只记录，连接的收口由链与传输层各自完成。
        /// </summary>
        /// <param name="id">连接标识</param>
        /// <param name="ex">异常</param>
        private void OnError(string id, Exception ex)
        {
            _ftpServer.Log("FTP数据通道异常:" + id, ex);
        }
    }
}