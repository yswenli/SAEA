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
*命名空间：SAEA.RPC.Net
*文件名： RServer
*版本号： v26.4.23.1
*唯一标识：9dacec5e-7d45-40ab-aed1-a431f27b1daa
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/05/25 17:28:26
*描述：RServer服务端类
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
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.RPC.Model;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.RPC.Net
{
    /// <summary>
    /// provider socket处理
    /// </summary>
    internal class RServer
    {
        private readonly IServerSocket _server;

        private readonly PipelineHostRegistry _host = new PipelineHostRegistry();

        private readonly ConcurrentDictionary<string, IChannelPipeline> _pipelines =
            new ConcurrentDictionary<string, IChannelPipeline>(StringComparer.Ordinal);

        // 业务回调需要用户令牌（反转执行时要传给过滤器），而处理器链只持有连接标识，这里按标识缓存。
        private readonly ConcurrentDictionary<string, IUserToken> _tokens =
            new ConcurrentDictionary<string, IUserToken>(StringComparer.Ordinal);

        private readonly ChannelPipelineOptions _options;

        /// <summary>
        /// 收到完整 RPC 消息时调用；返回的任务在本条消息处理完成后结束
        /// </summary>
        public Func<IUserToken, RSocketMsg, Task> MessageHandler { get; set; }

        /// <summary>
        /// 底层 socket 或链路异常时调用
        /// </summary>
        public Action<string, Exception> ErrorHandler { get; set; }

        private readonly object _configurePipelineLock = new object();

        private Action<string, IChannelPipeline> _configurePipeline;

        private bool _configurePipelineFrozen;

        /// <summary>
        /// 处理器链装配回调：每条连接在解码之后、异常收口之前调用一次，
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
        /// 构造函数，初始化RServer实例
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="maxConnects">最大连接数</param>
        public RServer(int port = 39654, int bufferSize = 64 * 1024, int maxConnects = 1000)
        {
            var readBufferSize = bufferSize > 0 ? bufferSize : 64 * 1024;

            // 单帧上限沿用 BaseCoder.MaxFrameLength 的可配置口径，输入残余上限按同一口径显式给出，
            // 避免自动推导在默认上限下超出 int 范围。
            var maxFrameLength = RpcFrameSplitter.ResolveMaxFrameLength();

            _options = new ChannelPipelineOptions
            {
                Splitter = RpcFrameSplitter.Create(maxFrameLength),
                MaxBufferedBytes = (long)maxFrameLength + FrameConstants.P_Head,
                ReadBufferSize = readBufferSize,
                // 请求与应答必须按到达顺序处理，同一连接串行执行。
                MaxConcurrentMessages = 1
            };

            var option = new SocketOptionBuilder()
                .SetSocket()
                .UseStream()
                .UseConnectionOpened(OnConnectionOpened)
                .UseInputHandler(RunPipelineAsync)
                .SetPort(port)
                .SetReadBufferSize(readBufferSize)
                .SetWriteBufferSize(readBufferSize)
                .SetMaxConnects(maxConnects)
                .Build();

            _server = SocketFactory.CreateServerSocket(option);

            _server.OnError += (id, ex) =>
            {
                var handler = ErrorHandler;
                if (handler != null)
                    handler(id, ex);
            };
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
        /// 接管一条连接的读循环：登记连接、装配处理器链并运行到收口
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

            _tokens[channelId] = userToken;

            var stream = new PipelineRawByteStream(channelId, reader, flow, _server);

            try
            {
                await PipelineWiring.RunAsync(
                    channelId,
                    stream,
                    flow,
                    _options,
                    token,
                    _host,
                    ConfigureConnection)
                    .ConfigureAwait(false);
            }
            finally
            {
                IChannelPipeline ignoredPipeline;
                _pipelines.TryRemove(channelId, out ignoredPipeline);

                IUserToken ignoredToken;
                _tokens.TryRemove(channelId, out ignoredToken);
            }
        }

        /// <summary>
        /// 装配单条连接的处理器链：出站前缀 → 解码 → RPC 协议分发 → 使用者扩展 → 链尾异常处理器
        /// </summary>
        /// <param name="channelId">连接标识</param>
        /// <param name="pipeline">尚未启动的处理器链</param>
        private void ConfigureConnection(string channelId, IChannelPipeline pipeline)
        {
            // 出站必须是链首连续前缀，串行器固定在最右，应答与通知才不会乱序。
            pipeline.AddLast(new RpcMessageEncoder());
            pipeline.AddLast(new OutboundSerializingHandler());

            pipeline.AddLast(new RpcMessageDecoder());
            pipeline.AddLast(new InboundDispatcher(this, channelId));

            var configure = ConfigurePipeline;
            if (configure != null)
                configure(channelId, pipeline);

            pipeline.AddLast(new ExceptionTerminalHandler());

            // 链装配完成才登记：主动应答只允许写到结构完整的链上。
            _pipelines[channelId] = pipeline;
        }

        /// <summary>
        /// 回复消息；连接尚未装配完成或已收口时静默丢弃，写出失败只记日志
        /// </summary>
        /// <param name="userToken">用户令牌</param>
        /// <param name="msg">消息对象</param>
        /// <returns>写出完成或失败后结束的任务</returns>
        internal async Task ReplyAsync(IUserToken userToken, RSocketMsg msg)
        {
            if (userToken == null || msg == null)
                return;

            IChannelPipeline pipeline;
            if (!_pipelines.TryGetValue(userToken.ID, out pipeline))
                return;

            try
            {
                await pipeline.WriteAsync(msg).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHelper.Warn("RPC 应答写出失败:" + userToken.ID, ex);
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="userToken">用户令牌</param>
        /// <returns>进入收口流程后即完成的任务</returns>
        internal Task DisconnectAsync(IUserToken userToken)
        {
            if (userToken == null || string.IsNullOrEmpty(userToken.ID))
                return Task.CompletedTask;

            return _host.CloseAsync(userToken.ID, "rpc-close");
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        internal void Start()
        {
            // 先冻结再监听：接受连接可能在 Start 返回前发生，冻结必须早于第一个连接建链。
            FreezeConfigurePipeline();

            _server.Start();
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        internal void Stop()
        {
            _server.Stop();
            _pipelines.Clear();
            _tokens.Clear();
        }

        /// <summary>
        /// 入站分发处理器：把解码后的消息交给业务回调，再原样传给下游使用者处理器。
        /// Close 消息只触发收口，不再向下游派发。
        /// </summary>
        private sealed class InboundDispatcher : InboundHandlerAdapter
        {
            private readonly RServer _owner;

            private readonly string _channelId;

            /// <summary>
            /// 绑定所属服务端与连接标识
            /// </summary>
            /// <param name="owner">所属服务端实例</param>
            /// <param name="channelId">连接标识</param>
            public InboundDispatcher(RServer owner, string channelId)
            {
                _owner = owner;
                _channelId = channelId;
            }

            /// <summary>
            /// 分发一条已解码的 RPC 消息；非 RPC 消息原样下传
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <param name="msg">已解码的消息</param>
            /// <returns>下游处理完成后结束的任务</returns>
            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var rpcMsg = msg as RSocketMsg;
                if (rpcMsg == null)
                {
                    await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
                    return;
                }

                IUserToken userToken;
                _owner._tokens.TryGetValue(_channelId, out userToken);

                var handler = _owner.MessageHandler;
                if (handler != null && userToken != null)
                {
                    try
                    {
                        await handler(userToken, rpcMsg).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await ctx.FireExceptionCaughtAsync(ex).ConfigureAwait(false);
                        return;
                    }
                }

                // Close 已经进入收口流程，继续下传只会让使用者面对一个正在关闭的连接。
                if ((RSocketMsgType)rpcMsg.Type == RSocketMsgType.Close)
                    return;

                await ctx.FireChannelReadAsync(rpcMsg).ConfigureAwait(false);
            }
        }
    }
}