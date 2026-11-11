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
*命名空间：SAEA.WebSocket.Core
*文件名： WSServerImpl
*版本号： v26.4.23.1
*唯一标识：dac162bb-ba5c-4a9d-b7e7-f2457a2327a1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/06/14 11:19:42
*描述：WSServerImpl类
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;
using SAEA.WebSocket.Model;
using SAEA.WebSocket.Type;

namespace SAEA.WebSocket.Core
{
    /// <summary>
    /// WebSocket 服务器，基于 SAEA.Sockets 的流式管线实现
    /// </summary>
    internal class WSServerImpl : IWSServer
    {
        private readonly IServerSocket _server;

        private readonly PipelineHostRegistry _host = new PipelineHostRegistry();

        private readonly ConcurrentDictionary<string, IChannelPipeline> _pipelines =
            new ConcurrentDictionary<string, IChannelPipeline>(StringComparer.Ordinal);

        private readonly object _clientsLock = new object();

        private readonly List<string> _clients = new List<string>();

        private readonly int _bufferSize;

        private readonly int _maxFrameLength;

        private readonly int _maxMessageSize;

        private readonly object _configurePipelineLock = new object();

        private Action<string, IChannelPipeline> _configurePipeline;

        private bool _configurePipelineFrozen;

        /// <summary>
        /// 处理器链装配回调：每条连接在帧解码器之后、异常收口之前调用一次，
        /// 使用者在此追加自己的入站处理器；为 null 时只保留内置处理器。
        /// 必须在 Start 之前赋值，Start 之后再赋值会抛 InvalidOperationException。
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
                        throw new InvalidOperationException("处理器链装配回调只能在 Start 之前赋值，服务端启动后已冻结。");

                    _configurePipeline = value;
                }
            }
        }

        /// <summary>
        /// 冻结装配回调，由 Start 调用
        /// </summary>
        private void FreezeConfigurePipeline()
        {
            lock (_configurePipelineLock)
            {
                _configurePipelineFrozen = true;
            }
        }

        /// <summary>
        /// 当前已建立的会话 ID 列表快照，每次读取返回独立副本，遍历期间不受连接增减影响
        /// </summary>
        public List<string> Clients
        {
            get
            {
                lock (_clientsLock)
                {
                    return new List<string>(_clients);
                }
            }
        }

        /// <summary>
        /// 初始化 WebSocket 服务器
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="maxConnects">连接数</param>
        /// <param name="sslProtocols">TLS 协议；为 None 或未提供证书时不启用加密</param>
        /// <param name="pfxPath">服务端证书路径</param>
        /// <param name="pwd">证书密码</param>
        public WSServerImpl(int port = 39654, int bufferSize = 1024 * 64, int maxConnects = 1000,
            SslProtocols sslProtocols = SslProtocols.None, string pfxPath = "", string pwd = "")
        {
            _bufferSize = bufferSize > 0 ? bufferSize : 1024 * 64;
            _maxMessageSize = WsFrameDecoder.DefaultMaxMessageSize;
            // 握手块与数据帧共用一把上限闸门，取两者较大值；屏障创建时固定，运行期不再改。
            _maxFrameLength = _maxMessageSize;

            var builder = new SocketOptionBuilder()
                .SetSocket()
                .UseStream()
                .UseConnectionOpened(OnConnectionOpened)
                .UseInputHandler(RunPipelineAsync)
                .SetPort(port)
                .SetReadBufferSize(_bufferSize)
                .SetWriteBufferSize(_bufferSize)
                .SetMaxConnects(maxConnects);

            // 只有同时给了协议和证书才启用 TLS；只给其一时退回明文，避免监听方以为已加密。
            if (sslProtocols != SslProtocols.None && !string.IsNullOrEmpty(pfxPath))
                builder.WithSsl(sslProtocols, pfxPath, pwd);

            _server = SocketFactory.CreateServerSocket(builder.Build());

            _server.OnError += (id, ex) => LogHelper.Warn("WebSocket:" + id, ex);
        }

        /// <summary>
        /// 连接门面登记钩子：处理器链只依赖连接标识，这里仅满足
        /// 「输入处理器必须与连接钩子成对配置」的约定。
        /// </summary>
        /// <param name="info">底层通道信息</param>
        private static void OnConnectionOpened(ChannelInfo info)
        {
        }

        /// <summary>
        /// 接管一条连接的读循环：按连接新建分帧器与屏障，装配处理器链并运行到收口
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
            var frameSplitter = new WsFrameSplitter(_maxFrameLength);
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

            var stream = new PipelineRawByteStream(channelId, reader, flow, _server);

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
                IChannelPipeline ignored;
                _pipelines.TryRemove(channelId, out ignored);
            }
        }

        /// <summary>
        /// 装配单条连接的处理器链：出站前缀 → 握手 → 帧解码 → 会话控制 → 使用者扩展 → 链尾异常处理器
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

            // 出站必须是链首连续前缀，串行器固定在最右，握手应答与业务帧才不会乱序。
            pipeline.AddLast(new WsMessageEncoder(false, state));
            pipeline.AddLast(new OutboundSerializingHandler());

            pipeline.AddLast(WsHandshakeHandler.CreateServer(barrier, frameSplitter, null, state));
            pipeline.AddLast(new WsFrameDecoder(true, _maxMessageSize));
            pipeline.AddLast(new SessionHandler(this, channelId));

            var configure = ConfigurePipeline;
            if (configure != null)
                configure(channelId, pipeline);

            pipeline.AddLast(new ExceptionTerminalHandler());

            // 链装配完成才登记：主动发送只允许写到结构完整的链上。
            _pipelines[channelId] = pipeline;
        }

        /// <summary>
        /// 会话建立：登记到客户端列表
        /// </summary>
        /// <param name="channelId">连接标识</param>
        private void OnSessionOpened(string channelId)
        {
            if (string.IsNullOrEmpty(channelId))
                return;

            lock (_clientsLock)
            {
                if (!_clients.Contains(channelId))
                    _clients.Add(channelId);
            }
        }

        /// <summary>
        /// 会话收口：从客户端列表移除
        /// </summary>
        /// <param name="channelId">连接标识</param>
        private void OnSessionClosed(string channelId)
        {
            if (string.IsNullOrEmpty(channelId))
                return;

            lock (_clientsLock)
            {
                _clients.Remove(channelId);
            }
        }

        /// <summary>
        /// 回复控制帧：帧体先复制再写出，调用方的接收帧可以立即归还池化缓冲
        /// </summary>
        /// <param name="channelId">连接标识</param>
        /// <param name="type">控制帧类型</param>
        /// <param name="content">控制帧帧体</param>
        /// <returns>写出完成或失败后结束的任务</returns>
        private Task ReplyControlAsync(string channelId, WSProtocalType type, ReadOnlyMemory<byte> content)
        {
            IChannelPipeline pipeline;
            if (!_pipelines.TryGetValue(channelId, out pipeline))
                return Task.CompletedTask;

            return WriteAndLogAsync(pipeline, channelId, new WSProtocal(type, content.ToArray()));
        }

        /// <summary>
        /// 写出协议帧并记录失败；主动发送不向业务线程抛出
        /// </summary>
        /// <param name="pipeline">目标连接的处理器链</param>
        /// <param name="channelId">连接标识</param>
        /// <param name="protocal">待写出的协议帧</param>
        /// <returns>写出完成或失败后结束的任务</returns>
        private static async Task WriteAndLogAsync(
            IChannelPipeline pipeline,
            string channelId,
            WSProtocal protocal)
        {
            try
            {
                await pipeline.WriteAsync(protocal).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHelper.Debug("WebSocket 发送失败:" + channelId + "," + ex.Message);
            }
        }

        /// <summary>
        /// 回复消息
        /// </summary>
        /// <param name="id">客户端会话 ID</param>
        /// <param name="data">协议数据</param>
        public void Reply(string id, WSProtocal data)
        {
            if (string.IsNullOrEmpty(id) || data == null)
                return;

            IChannelPipeline pipeline;
            if (!_pipelines.TryGetValue(id, out pipeline))
                return;

            _ = WriteAndLogAsync(pipeline, id, data);
        }

        /// <summary>
        /// 发送关闭消息并断开连接
        /// </summary>
        /// <param name="id">客户端会话 ID</param>
        /// <param name="data">协议数据</param>
        public void Disconnect(string id, WSProtocal data)
        {
            if (string.IsNullOrEmpty(id))
                return;

            Reply(id, data);
            _server.Disconnect(id);
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="id">客户端会话 ID</param>
        public void Disconnect(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;

            _server.Disconnect(id);
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        /// <param name="backlog">挂起连接队列的最大长度</param>
        public void Start(int backlog = 10000)
        {
            // 先冻结再监听：接受连接可能在 Start 返回前发生，冻结必须早于第一个连接建链。
            FreezeConfigurePipeline();

            _server.Start(backlog);
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public void Stop()
        {
            _server.Stop();

            _pipelines.Clear();

            lock (_clientsLock)
            {
                _clients.Clear();
            }
        }

        /// <summary>
        /// 会话控制处理器：维护会话登记，并在帧解码之后处理 Ping / Close 控制帧。
        /// 数据帧与 Pong 原样交给下游；Close 不回链，回写后直接有界收口。
        /// </summary>
        private sealed class SessionHandler : InboundHandlerAdapter
        {
            private readonly WSServerImpl _owner;

            private readonly string _channelId;

            /// <summary>
            /// 绑定所属服务端与连接标识
            /// </summary>
            /// <param name="owner">所属服务端实例</param>
            /// <param name="channelId">连接标识</param>
            public SessionHandler(WSServerImpl owner, string channelId)
            {
                _owner = owner;
                _channelId = channelId;
            }

            /// <summary>
            /// 连接激活时登记会话
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <returns>登记完成后结束的任务</returns>
            public override Task ChannelActiveAsync(IChannelHandlerContext ctx)
            {
                _owner.OnSessionOpened(_channelId);
                return Task.CompletedTask;
            }

            /// <summary>
            /// 连接收口时摘除会话
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <returns>清理完成后结束的任务</returns>
            public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
            {
                _owner.OnSessionClosed(_channelId);
                return Task.CompletedTask;
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
                            await _owner.ReplyControlAsync(_channelId, WSProtocalType.Pong, protocal.Content)
                                .ConfigureAwait(false);
                            return;

                        case (byte)WSProtocalType.Close:
                            await _owner.ReplyControlAsync(_channelId, WSProtocalType.Close, protocal.Content)
                                .ConfigureAwait(false);
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
        }
    }
}