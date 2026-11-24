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
 *命名空间：SAEA.Http.Base.Net
 *文件名： HttpSocket
 *版本号： v26.11.5.1
 *唯一标识：2e7d5c1b-8a3f-4d06-b4c9-6f1a9e0d2b73
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2018/10/28 14:19:50
 *描述：基于 SAEA.Sockets 流模式与处理器链的 HTTP 服务器实现
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/24 21:10:00
 *修改人： yswenli
 *版本号： v26.11.24.1
 *描述：协议处理器装配时传入请求体上限与 RequestDrainTimeout，支持 413 后有序排空
 *
 *****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.Http.Model;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;
using SAEA.Sockets.Ssl;

namespace SAEA.Http.Base.Net
{
    /// <summary>
    /// HTTP 服务器实现：每条连接在流模式下由处理器链接管读取，协议解析与业务派发都在链上完成。
    /// </summary>
    internal class HttpSocket : IHttpSocket
    {
        private const int DefaultReadBufferSize = 64 * 1024;

        // 100 Continue 是固定不变的协议级中间响应；只读共享，SendAsync 内部不会改写
        private static readonly byte[] _continueHead = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");

        private readonly IServerSocket _serverSokcet;

        private readonly ISocketOption _option;

        private readonly WebHost _webHost;

        private readonly PipelineHostRegistry _host = new PipelineHostRegistry();

        private readonly ConcurrentDictionary<string, ChannelInfo> _channels =
            new ConcurrentDictionary<string, ChannelInfo>(StringComparer.Ordinal);

        private readonly int _bufferSize;

        private readonly int _maxRequestBodySize;

        private readonly int _requestDrainTimeout;

        private readonly int _largeBodyThreshold;

        private readonly string _tempDirectory;

        private readonly bool _isDebug;

        /// <summary>
        /// 创建 HTTP 服务器。
        /// </summary>
        /// <param name="webHost">所属 Web 主机</param>
        /// <param name="port">监听端口</param>
        /// <param name="bufferSize">接收缓冲区字节数，默认 64KB</param>
        /// <param name="maxConnects">最大连接数，默认 1000</param>
        /// <param name="timeout">连接空闲超时秒数，默认 180</param>
        /// <param name="connectTimeout">连接建立超时秒数，默认 3</param>
        /// <param name="isDebug">是否输出调试日志</param>
        /// <param name="configureSsl">TLS 配置回调，null 表示保持明文 HTTP</param>
        internal HttpSocket(
            WebHost webHost,
            int port,
            int bufferSize = DefaultReadBufferSize,
            int maxConnects = 1000,
            double timeout = 180,
            double connectTimeout = 3,
            bool isDebug = false,
            Action<SaeaTlsOptions> configureSsl = null)
        {
            _webHost = webHost;
            _bufferSize = bufferSize > 0 ? bufferSize : DefaultReadBufferSize;
            _isDebug = isDebug;

            // 校验必须早于建链：否则端口已被占用时抛异常，监听端口会留在内核里没人释放。
            // 配置只留在 WebConfig 一处，避免再出现 HandleBufferSize 那种构造参数与配置各存一份的重复来源。
            // 上界按整帧缓冲的完整推导取：请求体上限 + 64KB 报头 + 9 字节帧头 + 读缓冲，
            // 执行器在 MaxBufferedBytes = 0 时也会这样推导，只改一半就会把溢出推迟到每条连接建链时才抛。
            var maxRequestBodySize = webHost.WebConfig.MaxRequestBodySize;

            if (maxRequestBodySize <= 0)
                throw new ArgumentOutOfRangeException(nameof(WebConfig.MaxRequestBodySize), "请求体上限必须大于 0。");

            if (maxRequestBodySize > int.MaxValue - 64 * 1024 - FrameConstants.P_Head - _bufferSize)
                throw new ArgumentOutOfRangeException(nameof(WebConfig.MaxRequestBodySize), "请求体上限与 64KB 报头上限、分帧附加开销（9 字节帧头 + 读缓冲）之和不能超过 int 上限。");

            _maxRequestBodySize = maxRequestBodySize;

            // 排空超时允许为 0 或负数：表示 413 后不排空，直接按普通关闭收口。
            _requestDrainTimeout = webHost.WebConfig.RequestDrainTimeout;

            var largeBodyThreshold = webHost.WebConfig.LargeBodyThreshold;

            if (largeBodyThreshold < 0)
                throw new ArgumentOutOfRangeException(nameof(WebConfig.LargeBodyThreshold), "请求体落盘阈值不能小于 0。");

            var tempDirectory = webHost.WebConfig.TempDirectory;

            if (string.IsNullOrWhiteSpace(tempDirectory))
                tempDirectory = Path.Combine(Path.GetTempPath(), "saea-http");

            try
            {
                // 幂等：已存在时直接返回；不可写时在启动期就暴露，而不是等第一条大请求才失败
                Directory.CreateDirectory(tempDirectory);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("请求体临时目录创建失败：" + tempDirectory, ex);
            }

            _largeBodyThreshold = largeBodyThreshold;

            _tempDirectory = tempDirectory;

            var optionBuilder = new SocketOptionBuilder()
               .SetSocket(SAEASocketType.Tcp)
               .UseStream()
               .UseConnectionOpened(OnConnectionOpened)
               .UseInputHandler(RunPipelineAsync)
               .SetPort(port)
               .SetMaxConnects(maxConnects)
               .SetReadBufferSize(bufferSize)
               .SetActionTimeOut((int)(timeout * 1000))
               .SetFreeTime((int)(timeout * 1000))
               .SetConnectTimeout((int)(connectTimeout * 1000))
               .ReusePort(false);

            // TLS 与流模式可共存：加密在底层完成，上层处理器链仍然只处理明文流。
            if (configureSsl != null)
                optionBuilder.WithSsl(configureSsl);

            _option = optionBuilder.Build();

            _serverSokcet = SocketFactory.CreateServerSocket(_option);

            _serverSokcet.OnError += (id, ex) => LogHelper.Error("HttpSocket:" + id, ex);
            _serverSokcet.OnDisconnected += (id, ex) => RemoveChannel(id);
        }

        /// <summary>
        /// 记录连接信息，供管线激活时建立会话使用。
        /// </summary>
        /// <param name="info">底层通道信息</param>
        private void OnConnectionOpened(ChannelInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.ID))
                return;

            _channels[info.ID] = info;
        }

        /// <summary>
        /// 接管一条连接的读循环：装配处理器链并运行到收口。
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

            ChannelInfo info;
            _channels.TryGetValue(channelId, out info);

            // 先有 HTTP 分帧器，再有屏障，最后回填引用，打破「分帧器要屏障、屏障要分帧器」的构造循环。
            var httpSplitter = new HttpRequestSplitter(maxRequestSize: _maxRequestBodySize);

            var upgradedSplitter = new TakeAvailableSplitter(_bufferSize);

            var barrier = new SwitchableSplitter(httpSplitter, upgradedSplitter);

            httpSplitter.Barrier = barrier;

            var options = new ChannelPipelineOptions
            {
                Splitter = barrier,
                // 留 0 由执行器按 MaxFrameLength + P_Head + ReadBufferSize 推导，
                // 保证一整帧请求加上本次读取的余量都能进缓冲；请求体上限变化时推导值随之变化。
                MaxBufferedBytes = 0,
                ReadBufferSize = _bufferSize,
                // HTTP 是单工请求/响应：同一连接必须串行处理，否则保活连接的响应会乱序。
                MaxConcurrentMessages = 1,
                RequireTerminalHandler = true
            };

            var stream = new PipelineRawByteStream(channelId, reader, flow, _serverSokcet);

            try
            {
                await PipelineWiring.RunAsync(
                    channelId,
                    stream,
                    flow,
                    options,
                    token,
                    _host,
                    (id, pipeline) => ConfigurePipeline(id, pipeline, info, barrier, upgradedSplitter))
                    .ConfigureAwait(false);
            }
            finally
            {
                RemoveChannel(channelId);
            }
        }

        /// <summary>
        /// 装配单条连接的处理器链：出站前缀 → 入站协议链 → 使用者扩展 → 链尾异常处理器。
        /// </summary>
        /// <param name="channelId">连接标识</param>
        /// <param name="pipeline">尚未启动的处理器链</param>
        /// <param name="info">底层通道信息</param>
        /// <param name="barrier">协议切换屏障</param>
        /// <param name="upgradedSplitter">升级后生效的分帧器</param>
        private void ConfigurePipeline(
            string channelId,
            IChannelPipeline pipeline,
            ChannelInfo info,
            SwitchableSplitter barrier,
            IFrameSplitter upgradedSplitter)
        {
            // 100 Continue 必须绕过出站处理链直发：它是协议级中间响应，不参与响应编码与串行化。
            var httpSplitter = barrier == null ? null : barrier.Current as HttpRequestSplitter;
            if (httpSplitter != null)
                httpSplitter.ExpectContinueHandler = () => SendContinue(channelId);

            // 出站必须是链首连续前缀，串行器固定在最右，响应顺序才不会被异步编码打乱。
            pipeline.AddLast(new HttpResponseEncoder());
            pipeline.AddLast(new OutboundSerializingHandler());

            pipeline.AddLast(new HttpRequestDecoder(_largeBodyThreshold, _tempDirectory, channelId));
            pipeline.AddLast(new HttpProtocolHandler(barrier, upgradedSplitter, null, _maxRequestBodySize, _requestDrainTimeout));
            pipeline.AddLast(new HttpDispatchHandler(_webHost, info, barrier, upgradedSplitter, _isDebug));
            pipeline.AddLast(new HttpRawFrameSinkHandler());

            var configure = _webHost.WebConfig == null ? null : _webHost.WebConfig.ConfigurePipeline;
            if (configure != null)
                configure(channelId, pipeline);

            pipeline.AddLast(new ExceptionTerminalHandler());
        }

        /// <summary>
        /// 直发 100 Continue 中间响应。走底层 server socket，不经过出站处理链；TLS 连接由 SendAsync 内部处理加密。
        /// </summary>
        /// <param name="channelId">连接标识</param>
        private void SendContinue(string channelId)
        {
            try
            {
                _serverSokcet.SendAsync(channelId, _continueHead);
            }
            catch (Exception ex)
            {
                // 100 Continue 只是加速提示，发送失败按普通请求继续处理
                LogHelper.Error("HttpSocket.SendContinue:" + channelId, ex);
            }
        }

        /// <summary>
        /// 移除连接信息。
        /// </summary>
        /// <param name="id">连接标识</param>
        private void RemoveChannel(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;

            ChannelInfo ignored;
            _channels.TryRemove(id, out ignored);
        }

        /// <summary>
        /// 设置/取消会话的空闲超时豁免
        /// </summary>
        /// <param name="id">会话ID</param>
        /// <param name="noTimeout">true 表示不参与空闲清理</param>
        public void SetNoTimeout(string id, bool noTimeout)
        {
            _serverSokcet.SessionManager.SetNoTimeout(id, noTimeout);
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        public void Start()
        {
            _serverSokcet.Start();
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public void Stop()
        {
            _serverSokcet.Stop();
        }
    }
}