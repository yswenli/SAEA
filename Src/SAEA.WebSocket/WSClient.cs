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
*命名空间：SAEA.WebSocket
*文件名： WSClient
*版本号： v26.4.23.1
*唯一标识：b9e5d76a-0843-4382-bfb8-989aaf016297
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：WSClient接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 11:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：装配回调改为启动前注入：Start/Connect 之后再赋值抛 InvalidOperationException，中英 README 同步说明
*
*****************************************************************************/
using System;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;
using SAEA.Sockets.Ssl;
using SAEA.WebSocket.Model;
using SAEA.WebSocket.Type;

namespace SAEA.WebSocket
{
    /// <summary>
    /// WebSocket 客户端，负责握手、收发帧与心跳应答
    /// </summary>
    public class WSClient
    {
        string _url;

        string _serverIP;

        int _serverPort;

        volatile bool _isHandSharked = false;

        string _subProtocol = SubProtocolType.Default;

        string _origin;

        private readonly object _configurePipelineLock = new object();

        private Action<string, IChannelPipeline> _configurePipeline;

        private bool _configurePipelineFrozen;

        /// <summary>
        /// 处理器链装配回调：连接建立、握手处理器之后、异常收口之前调用一次，
        /// 接收会话 ID 与尚未启动的链；为 null 时只保留内置处理器。
        /// 必须在首次 Connect 之前赋值，连接建立后再赋值会抛 InvalidOperationException。
        /// </summary>
        public Action<string, IChannelPipeline> ConfigurePipeline
        {
            get
            {
                lock (_configurePipelineLock)
                {
                    return _configurePipeline;
                }
            }
            set
            {
                lock (_configurePipelineLock)
                {
                    if (_configurePipelineFrozen)
                        throw new InvalidOperationException("处理器链装配回调只能在首次 Connect 之前赋值，连接建立后已冻结。");

                    _configurePipeline = value;
                }
            }
        }

        /// <summary>
        /// 冻结装配回调，由 Connect 调用
        /// </summary>
        private void FreezeConfigurePipeline()
        {
            lock (_configurePipelineLock)
            {
                _configurePipelineFrozen = true;
            }
        }

        IClientSocket _client;

        readonly PipelineHostRegistry _host = new PipelineHostRegistry();

        readonly ManualResetEventSlim _handshakeDone = new ManualResetEventSlim(false);

        volatile IChannelPipeline _pipeline;

        int _maxMessageSize;

        int _bufferSize;

        /// <summary>
        /// 初始化 WSClient 类的新实例
        /// </summary>
        /// <param name="ip">服务器IP地址</param>
        /// <param name="port">服务器端口</param>
        /// <param name="subProtocol">子协议</param>
        /// <param name="origin">请求来源</param>
        /// <param name="bufferSize">缓冲区大小</param>
        public WSClient(string ip = "127.0.0.1", int port = 39654, string subProtocol = SubProtocolType.Default, string origin = "", int bufferSize = 64 * 1024)
            : this(ip, port, subProtocol, origin, bufferSize, null)
        {
        }

        /// <summary>
        /// 初始化 WSClient 类的新实例，并启用 TLS
        /// </summary>
        /// <param name="ip">服务器IP地址</param>
        /// <param name="port">服务器端口</param>
        /// <param name="subProtocol">子协议</param>
        /// <param name="origin">请求来源</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="configureTls">TLS 配置回调，直接作用于底层 socket 的 TLS 选项；非 null 即启用加密</param>
        public WSClient(string ip, int port, string subProtocol, string origin, int bufferSize, Action<SaeaTlsOptions> configureTls)
        {
            _serverIP = ip;
            _serverPort = port;

            if (string.IsNullOrEmpty(_url))
                _url = $"ws://{ip}:{port}/";

            _origin = origin;

            _subProtocol = subProtocol;

            _bufferSize = bufferSize > 0 ? bufferSize : 64 * 1024;
            _maxMessageSize = WsFrameDecoder.DefaultMaxMessageSize;

            var builder = new SocketOptionBuilder()
                .SetSocket()
                .UseStream()
                .UseConnectionOpened(OnConnectionOpened)
                .UseInputHandler(RunPipelineAsync)
                .SetIP(ip)
                .SetPort(port)
                .SetReadBufferSize(_bufferSize)
                .SetWriteBufferSize(_bufferSize);

            if (configureTls != null)
                builder.WithSsl(configureTls);

            _client = SocketFactory.CreateClientSocket(builder.Build());

            _client.OnError += (id, ex) => LogHelper.Warn("WebSocket Client:" + id, ex);
        }

        /// <summary>
        /// 初始化 WSClient 类的新实例
        /// </summary>
        /// <param name="uri">服务器URI</param>
        /// <param name="subProtocol">子协议</param>
        /// <param name="origin">请求来源</param>
        public WSClient(Uri uri, string subProtocol = SubProtocolType.Default, string origin = "") : this(uri.Host, uri.Port, subProtocol, origin)
        {
            _url = uri.ToString();
        }

        /// <summary>
        /// 初始化 WSClient 类的新实例，并启用 TLS
        /// </summary>
        /// <param name="uri">服务器URI</param>
        /// <param name="subProtocol">子协议</param>
        /// <param name="origin">请求来源</param>
        /// <param name="configureTls">TLS 配置回调，非 null 即启用加密</param>
        public WSClient(Uri uri, string subProtocol, string origin, Action<SaeaTlsOptions> configureTls)
            : this(uri.Host, uri.Port, subProtocol, origin, 64 * 1024, configureTls)
        {
            _url = uri.ToString();
        }

        /// <summary>
        /// 初始化 WSClient 类的新实例
        /// </summary>
        /// <param name="url">服务器URL</param>
        /// <param name="subProtocol">子协议</param>
        /// <param name="origin">请求来源</param>
        public WSClient(string url, string subProtocol = SubProtocolType.Default, string origin = "") : this(new Uri(url), subProtocol, origin)
        {
            _url = url;
        }

        /// <summary>
        /// 初始化 WSClient 类的新实例，并启用 TLS
        /// </summary>
        /// <param name="url">服务器URL</param>
        /// <param name="subProtocol">子协议</param>
        /// <param name="origin">请求来源</param>
        /// <param name="configureTls">TLS 配置回调，非 null 即启用加密</param>
        public WSClient(string url, string subProtocol, string origin, Action<SaeaTlsOptions> configureTls)
            : this(new Uri(url), subProtocol, origin, configureTls)
        {
            _url = url;
        }

        /// <summary>
        /// 连接门面登记钩子：处理器链只依赖会话标识，这里仅满足
        /// 「输入处理器必须与连接钩子成对配置」的约定。
        /// </summary>
        /// <param name="info">底层通道信息</param>
        private static void OnConnectionOpened(ChannelInfo info)
        {
        }

        /// <summary>
        /// 接管本连接的读循环：按连接新建分帧器与屏障，装配处理器链并运行到收口
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

            // 分帧器与屏障都是连接级状态（握手块、切换点各自独立），不能跨连接共享。
            var handshakeSplitter = new WsHandshakeSplitter();
            var frameSplitter = new WsFrameSplitter(_maxMessageSize);
            var barrier = new SwitchableSplitter(handshakeSplitter, frameSplitter);
            handshakeSplitter.Barrier = barrier;

            var options = new ChannelPipelineOptions
            {
                Splitter = barrier,
                MaxBufferedBytes = (long)barrier.MaxFrameLength + FrameConstants.P_Head,
                ReadBufferSize = _bufferSize,
                // 同一连接上的帧必须按到达顺序处理，否则分片重组与业务顺序都会乱。
                MaxConcurrentMessages = 1
            };

            var stream = new PipelineRawByteStream(
                channelId,
                reader,
                flow,
                (data, owner) => _client.SendAsync(data, owner));

            try
            {
                await PipelineWiring.RunAsync(
                    channelId,
                    stream,
                    flow,
                    options,
                    token,
                    _host,
                    (id, pipeline) => ConfigureConnection(id, pipeline, barrier, frameSplitter))
                    .ConfigureAwait(false);
            }
            finally
            {
                _pipeline = null;

                // 收口后仍在等待握手的调用方不能一直挂到超时。
                _handshakeDone.Set();
            }
        }

        /// <summary>
        /// 装配本连接的处理器链：出站前缀 → 握手 → 帧解码 → 会话控制 → 使用者扩展 → 链尾异常处理器
        /// </summary>
        /// <param name="channelId">连接标识</param>
        /// <param name="pipeline">尚未启动的处理器链</param>
        /// <param name="barrier">该连接的协议切换屏障</param>
        /// <param name="frameSplitter">握手成功后生效的帧分帧器</param>
        private void ConfigureConnection(
            string channelId,
            IChannelPipeline pipeline,
            SwitchableSplitter barrier,
            IFrameSplitter frameSplitter)
        {
            var state = new WsHandshakeState();

            var uri = new Uri(_url);
            var target = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
            var host = _serverPort == 80 ? _serverIP : _serverIP + ":" + _serverPort;

            // 出站必须是链首连续前缀，串行器固定在最右，握手请求与业务帧才不会乱序。
            pipeline.AddLast(new WsMessageEncoder(true, state));
            pipeline.AddLast(new OutboundSerializingHandler());

            pipeline.AddLast(WsHandshakeHandler.CreateClient(
                barrier,
                frameSplitter,
                target,
                host,
                _subProtocol,
                _origin,
                OnHandshakeCompleted,
                state));

            pipeline.AddLast(new WsFrameDecoder(false, _maxMessageSize));
            pipeline.AddLast(new ClientSessionHandler(this));
            pipeline.AddLast(new ActiveHandler(this));

            var configure = ConfigurePipeline;
            if (configure != null)
                configure(channelId, pipeline);

            pipeline.AddLast(new ExceptionTerminalHandler());
        }

        /// <summary>
        /// 握手成功：置位连接状态并放开正在等待的 Connect 调用
        /// </summary>
        /// <param name="channelId">连接标识</param>
        private void OnHandshakeCompleted(string channelId)
        {
            _isHandSharked = true;
            _handshakeDone.Set();
        }

        /// <summary>
        /// 连接WebSocket服务器
        /// </summary>
        /// <param name="timeOut">超时时间</param>
        /// <returns>是否连接成功</returns>
        public bool Connect(int timeOut = 10 * 1000)
        {
            // 首次连接即冻结装配回调：后续重连沿用同一份配置，避免已建链的连接与回调不一致。
            FreezeConfigurePipeline();

            if (_client.Connected && _isHandSharked)
                return true;

            _isHandSharked = false;

            // 先复位再发起连接：握手可能在连接回调返回前就完成。
            _handshakeDone.Reset();

            _client.ConnectAsync(e =>
            {
                if (e != SocketError.Success)
                {
                    LogHelper.Debug("WebSocket Client 连接失败:" + _serverIP + ":" + _serverPort + "," + e);
                    _handshakeDone.Set();
                }
            });

            var signalled = _handshakeDone.Wait(timeOut);

            if (signalled && _isHandSharked)
                return true;

            _client.Disconnect();

            return false;
        }

        /// <summary>
        /// 发送数据到WebSocket服务器
        /// </summary>
        /// <param name="msg">要发送的数据</param>
        public void SendBase(ISocketProtocal msg)
        {
            var pipeline = _pipeline;
            if (pipeline == null || msg == null)
                return;

            _ = WriteAndLogAsync(pipeline, msg);
        }

        /// <summary>
        /// 发送数据到WebSocket服务器
        /// </summary>
        /// <param name="msg">要发送的数据</param>
        /// <param name="type">数据类型</param>
        public void Send(byte[] msg, WSProtocalType type = WSProtocalType.Text)
        {
            SendBase(new WSProtocal(type, msg));
        }

        /// <summary>
        /// 发送文本数据到WebSocket服务器
        /// </summary>
        /// <param name="msg">要发送的文本数据</param>
        public void Send(string msg)
        {
            Send(Encoding.UTF8.GetBytes(msg));
        }

        /// <summary>
        /// 回复服务器心跳
        /// </summary>
        private void ReplyPong()
        {
            SendBase(new WSProtocal(WSProtocalType.Pong, null));
        }

        /// <summary>
        /// 发送Ping包
        /// </summary>
        public void Ping()
        {
            var msg = DateTimeHelper.ToString();
            SendBase(new WSProtocal(WSProtocalType.Ping, Encoding.UTF8.GetBytes(msg)));
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        /// <param name="discription">关闭描述</param>
        public void Close(string discription = "客户端主动断开ws连接")
        {
            var pipeline = _pipeline;
            if (pipeline == null)
            {
                _client.Disconnect();
                return;
            }

            _ = CloseAsync(pipeline, discription);
        }

        /// <summary>
        /// 先写关闭帧再请求收口；写入失败同样要收口，避免连接悬空
        /// </summary>
        /// <param name="pipeline">目标连接的处理器链</param>
        /// <param name="discription">关闭描述</param>
        /// <returns>收口请求发出后结束的任务</returns>
        private static async Task CloseAsync(IChannelPipeline pipeline, string discription)
        {
            try
            {
                await pipeline.WriteAsync(new WSProtocal(
                    WSProtocalType.Close,
                    Encoding.UTF8.GetBytes(discription ?? string.Empty))).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHelper.Debug("WebSocket Client 发送关闭帧失败:" + ex.Message);
            }
            finally
            {
                try
                {
                    await pipeline.CloseAsync("ws-client-close").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogHelper.Debug("WebSocket Client 收口失败:" + ex.Message);
                }
            }
        }

        /// <summary>
        /// 写出协议帧并记录失败；主动发送不向业务线程抛出
        /// </summary>
        /// <param name="pipeline">目标连接的处理器链</param>
        /// <param name="msg">待写出的协议帧</param>
        /// <returns>写出完成或失败后结束的任务</returns>
        private static async Task WriteAndLogAsync(IChannelPipeline pipeline, ISocketProtocal msg)
        {
            try
            {
                await pipeline.WriteAsync(msg).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHelper.Debug("WebSocket Client 发送失败:" + ex.Message);
            }
        }

        /// <summary>
        /// 会话控制处理器：应答服务端 Ping、回写 Close 后断开；数据帧与 Pong 原样交给下游
        /// </summary>
        private sealed class ClientSessionHandler : InboundHandlerAdapter
        {
            private readonly WSClient _owner;

            /// <summary>
            /// 绑定所属客户端
            /// </summary>
            /// <param name="owner">所属客户端实例</param>
            public ClientSessionHandler(WSClient owner)
            {
                _owner = owner;
            }

            /// <summary>
            /// 处理控制帧；数据帧与 Pong 继续交给下游。帧体处理完即归还池化缓冲。
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <param name="msg">已解帧的消息</param>
            /// <returns>处理完成后结束的任务</returns>
            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var protocal = msg as WSProtocal;
                if (protocal == null)
                {
                    await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
                    return;
                }

                try
                {
                    switch (protocal.Type)
                    {
                        case (byte)WSProtocalType.Ping:
                            _owner.ReplyPong();
                            return;

                        case (byte)WSProtocalType.Close:
                            await ReplyCloseAsync(ctx, protocal.Content).ConfigureAwait(false);
                            await ctx.CloseAfterWriteAsync("ws-close-frame").ConfigureAwait(false);
                            return;

                        default:
                            await ctx.FireChannelReadAsync(protocal).ConfigureAwait(false);
                            return;
                    }
                }
                finally
                {
                    // 帧体的池化缓冲在本次处理结束后归还；下游需要跨回调保留时必须自行复制。
                    protocal.Dispose();
                }
            }

            /// <summary>
            /// 回写关闭帧；失败只影响收口时序，不覆盖原始关闭原因
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <param name="content">对端关闭帧的内容</param>
            /// <returns>回写完成或失败后结束的任务</returns>
            private static async Task ReplyCloseAsync(IChannelHandlerContext ctx, ReadOnlyMemory<byte> content)
            {
                try
                {
                    // 帧体先复制再写出，调用方的接收帧可以立即归还池化缓冲。
                    await ctx.Pipeline.WriteAsync(new WSProtocal(WSProtocalType.Close, content.ToArray()))
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogHelper.Debug("WebSocket Client 回写关闭帧失败:" + ex.Message);
                }
            }
        }

        /// <summary>
        /// 连接激活处理器：登记本连接的写入口，并在失活时清空
        /// </summary>
        private sealed class ActiveHandler : InboundHandlerAdapter
        {
            private readonly WSClient _owner;

            /// <summary>
            /// 绑定所属客户端
            /// </summary>
            /// <param name="owner">所属客户端实例</param>
            public ActiveHandler(WSClient owner)
            {
                _owner = owner;
            }

            /// <summary>
            /// 连接启动时登记写入口，供业务线程发起发送
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <returns>登记完成后结束的任务</returns>
            public override Task ChannelActiveAsync(IChannelHandlerContext ctx)
            {
                _owner._pipeline = ctx.Pipeline;
                return Task.CompletedTask;
            }

            /// <summary>
            /// 连接收口时清空写入口并放开等待握手的调用方
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <returns>清理完成后结束的任务</returns>
            public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
            {
                _owner._pipeline = null;
                _owner._handshakeDone.Set();
                return Task.CompletedTask;
            }

            /// <summary>
            /// 握手失败等异常同样要放开等待握手的调用方，不能让它一直挂到超时
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <param name="ex">待传播的异常</param>
            /// <returns>下游处理完成后结束的任务</returns>
            public override async Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
            {
                _owner._handshakeDone.Set();
                await ctx.FireExceptionCaughtAsync(ex).ConfigureAwait(false);
            }
        }
    }
}