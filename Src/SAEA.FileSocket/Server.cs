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
*命名空间：SAEA.FileSocket
*文件名： Server
*版本号： v26.4.23.1
*唯一标识：507c5630-17bc-479d-a611-7870e09ead35
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：Server服务端类
*
*=====================================================================
*修改标记
*修改时间：2026/11/16 10:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：通道链配置改为每连接新建，并显式开启启动期链尾校验
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Serialization;
using SAEA.FileSocket.Model;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.FileSocket
{
    /// <summary>
    /// 服务器
    /// 采用默认的上下文操作
    /// </summary>
    public class Server
    {
        private long _total;

        private long _in;

        /// <summary>
        /// 最近一次传输请求声明的文件总长度（字节）
        /// </summary>
        public long Total { get => _total; set => _total = value; }

        /// <summary>
        /// 已接收的字节数，在接收回调中以原子方式累加
        /// </summary>
        public long In { get => _in; set => _in = value; }

        private readonly object _configurePipelineLock = new object();

        private Action<string, IChannelPipeline> _configurePipeline;

        private bool _configurePipelineFrozen;

        /// <summary>
        /// 处理器链装配回调：每条连接在解码器之后、异常收口之前调用一次，
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

        private readonly IServerSocket _server;

        private readonly PipelineHostRegistry _host = new PipelineHostRegistry();

        private readonly ConcurrentDictionary<string, IChannelPipeline> _pipelines =
            new ConcurrentDictionary<string, IChannelPipeline>(StringComparer.Ordinal);

        // 单帧上限与读缓冲大小在构造时解析一次，之后每条连接按同一口径新建分帧器与配置。
        private readonly int _maxFrameLength;

        private readonly int _readBufferSize;

        /// <summary>
        /// 创建一条连接的通道链配置：配置与分帧器实例不跨连接复用，
        /// 分帧器一旦引入连接级状态（如切换式分帧），共享实例会让多连接互相干扰
        /// </summary>
        /// <returns>尚未被任何链使用的独立配置实例</returns>
        private ChannelPipelineOptions CreateOptions()
        {
            return new ChannelPipelineOptions
            {
                Splitter = FileFrameSplitter.Create(_maxFrameLength),
                MaxBufferedBytes = (long)_maxFrameLength + FrameConstants.P_Head,
                ReadBufferSize = _readBufferSize,
                // 文件块按顺序落盘，同一连接必须串行执行，否则分块会乱序写入。
                MaxConcurrentMessages = 1,
                RequireTerminalHandler = true
            };
        }

        /// <summary>
        /// 创建底层服务端并注册收连接回调，监听需另调 Start
        /// </summary>
        /// <param name="port">监听端口</param>
        /// <param name="bufferSize">收发缓冲区大小（字节）</param>
        /// <param name="count">最大连接数</param>
        public Server(int port = 39654, int bufferSize = 100 * 1024, int count = 10)
        {
            var readBufferSize = bufferSize > 0 ? bufferSize : 100 * 1024;

            // 单帧上限沿用 BaseCoder.MaxFrameLength 的可配置口径，输入残余上限按同一口径显式给出，
            // 避免自动推导在默认上限下超出 int 范围。
            _maxFrameLength = FileFrameSplitter.ResolveMaxFrameLength();

            _readBufferSize = readBufferSize;

            var option = new SocketOptionBuilder()
                .SetSocket()
                .UseStream()
                .UseConnectionOpened(OnConnectionOpened)
                .UseInputHandler(RunPipelineAsync)
                .SetPort(port)
                .ReusePort(false)
                .SetReadBufferSize(readBufferSize)
                .SetWriteBufferSize(readBufferSize)
                .SetMaxConnects(count)
                .Build();

            _server = SocketFactory.CreateServerSocket(option);

            _server.OnError += (id, ex) => LogHelper.Warn("FileSocket:" + id, ex);
        }

        /// <summary>
        /// 连接门面登记钩子：文件传输链只依赖连接标识，不需要连接级信息，
        /// 这里仅满足「输入处理器必须与连接钩子成对配置」的约定。
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

            var stream = new PipelineRawByteStream(channelId, reader, flow, _server);

            try
            {
                await PipelineWiring.RunAsync(
                    channelId,
                    stream,
                    flow,
                    CreateOptions(),
                    token,
                    _host,
                    ConfigureConnection)
                    .ConfigureAwait(false);
            }
            finally
            {
                IChannelPipeline ignored;
                _pipelines.TryRemove(channelId, out ignored);
            }
        }

        /// <summary>
        /// 装配单条连接的处理器链：出站前缀 → 内置入站 → 使用者扩展 → 链尾异常处理器
        /// </summary>
        /// <param name="channelId">连接标识</param>
        /// <param name="pipeline">尚未启动的处理器链</param>
        private void ConfigureConnection(string channelId, IChannelPipeline pipeline)
        {
            // 出站必须是链首连续前缀，串行器固定在最右，允许接收与拒绝应答才不会乱序。
            pipeline.AddLast(new FileMessageEncoder());
            pipeline.AddLast(new OutboundSerializingHandler());

            pipeline.AddLast(new FileMessageDecoder());
            pipeline.AddLast(new TrafficHandler(this));

            var configure = ConfigurePipeline;
            if (configure != null)
                configure(channelId, pipeline);

            pipeline.AddLast(new ExceptionTerminalHandler());

            // 链装配完成才登记：主动应答只允许写到结构完整的链上。
            _pipelines[channelId] = pipeline;
        }

        /// <summary>
        /// 允许指定会话开始发送文件，向该会话回送允许接收协议包
        /// </summary>
        /// <param name="id">会话标识</param>
        public void Allow(string id)
        {
            Send(id, SocketProtocalType.AllowReceive);
        }

        /// <summary>
        /// 拒绝指定会话的文件传输请求，向该会话回送拒绝接收协议包
        /// </summary>
        /// <param name="id">会话标识</param>
        public void Refuse(string id)
        {
            Send(id, SocketProtocalType.RefuseReceive);
        }

        /// <summary>
        /// 向指定会话写一个空帧体应答；连接尚未装配完成或已断开时静默丢弃
        /// </summary>
        /// <param name="id">会话标识</param>
        /// <param name="type">应答帧类型</param>
        private void Send(string id, SocketProtocalType type)
        {
            if (string.IsNullOrEmpty(id))
                return;

            IChannelPipeline pipeline;
            if (!_pipelines.TryGetValue(id, out pipeline))
                return;

            _ = SendCoreAsync(pipeline, id, type);
        }

        /// <summary>
        /// 异步写出应答并记录失败；应答是通知性质，不向业务线程抛出
        /// </summary>
        /// <param name="pipeline">目标连接的处理器链</param>
        /// <param name="id">会话标识</param>
        /// <param name="type">应答帧类型</param>
        /// <returns>写出完成或失败后结束的任务</returns>
        private static async Task SendCoreAsync(IChannelPipeline pipeline, string id, SocketProtocalType type)
        {
            try
            {
                await pipeline.WriteAsync(new BaseSocketProtocal((byte)type, ReadOnlyMemory<byte>.Empty))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHelper.Warn("FileSocket 应答写出失败:" + id, ex);
            }
        }

        /// <summary>
        /// 启动服务端并开始监听
        /// </summary>
        public void Start()
        {
            // 先冻结再监听：接受连接可能在 Start 返回前发生，冻结必须早于第一个连接建链。
            FreezeConfigurePipeline();

            _server.Start();
        }

        /// <summary>
        /// 停止服务端并关闭现有连接
        /// </summary>
        public void Stop()
        {
            _server.Stop();
            _pipelines.Clear();
        }

        /// <summary>
        /// 内置入站处理器：累计已接收的文件字节，并从传输请求中记录声明的文件总长度
        /// </summary>
        private sealed class TrafficHandler : InboundHandlerAdapter
        {
            private readonly Server _owner;

            /// <summary>
            /// 绑定所属服务端
            /// </summary>
            /// <param name="owner">所属服务端实例</param>
            public TrafficHandler(Server owner)
            {
                _owner = owner;
            }

            /// <summary>
            /// 统计文件块字节并解析传输请求的声明长度，然后把消息继续交给下游
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <param name="msg">已解帧的消息</param>
            /// <returns>下游处理完成后结束的任务</returns>
            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var protocal = msg as BaseSocketProtocal;
                if (protocal != null)
                {
                    if (protocal.Type == (byte)SocketProtocalType.BigData)
                    {
                        Interlocked.Add(ref _owner._in, protocal.Content.Length);
                    }
                    else if (protocal.Type == (byte)SocketProtocalType.RequestSend && !protocal.Content.IsEmpty)
                    {
                        try
                        {
                            _owner._total = SerializeHelper.PBDeserialize<FileMessage>(protocal.Content.ToArray()).Length;
                        }
                        catch (Exception ex)
                        {
                            // 请求体不是合法的文件消息：长度统计保持原值，交给下游决定如何处理。
                            LogHelper.Debug("FileSocket 传输请求解析失败:" + ex.Message);
                        }
                    }
                }

                await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
            }
        }
    }
}