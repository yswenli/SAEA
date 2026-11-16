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
*文件名： Client
*版本号： v26.4.23.1
*唯一标识：2a41eb6e-03dc-4ae0-bd0e-9cde3876dae3
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：Client接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/16 10:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：通道链配置显式开启启动期链尾校验
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Common.Serialization;
using SAEA.FileSocket.Model;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.FileSocket
{
    /// <summary>
    /// 文件传输客户端，按流模式连接服务器并按块发送文件，构造后自动启动心跳线程
    /// </summary>
    public class Client
    {
        /// <summary>
        /// 等待处理器链接管连接的上限（毫秒）；超时只放弃等待，后续发送会因链未就绪而失败
        /// </summary>
        private const int PipelineReadyTimeout = 5000;

        /// <summary>
        /// 心跳间隔（毫秒）
        /// </summary>
        private const int HeartSpan = 10 * 1000;

        private DateTime _actived;

        private readonly int _bufferSize;

        private long _total;

        private long _out;

        /// <summary>
        /// 待发送文件的总字节数，发送前由文件长度赋值
        /// </summary>
        public long Total { get => _total; set => _total = value; }

        /// <summary>
        /// 已发送的字节数，发送过程中以原子方式累加
        /// </summary>
        public long Out { get => _out; set => _out = value; }

        /// <summary>
        /// 与服务器的连接是否已建立
        /// </summary>
        public bool Connected { get { return _client.Connected; } }

        private readonly ConcurrentStack<Action<bool>> _eventCollection = new ConcurrentStack<Action<bool>>();

        private readonly AutoResetEvent _autoResetEvent = new AutoResetEvent(true);

        private readonly ManualResetEventSlim _pipelineReady = new ManualResetEventSlim(false);

        private readonly PipelineHostRegistry _host = new PipelineHostRegistry();

        private readonly ChannelPipelineOptions _options;

        private volatile IChannelPipeline _pipeline;

        private readonly IClientSocket _client;

        /// <summary>
        /// 创建客户端并装配处理器链，同时启动后台心跳线程
        /// </summary>
        /// <param name="bufferSize">收发缓冲区大小，同时也是文件分块读取的大小（字节）</param>
        /// <param name="ip">服务器地址</param>
        /// <param name="port">服务器端口</param>
        public Client(int bufferSize = 100 * 1024, string ip = "127.0.0.1", int port = 39654)
        {
            _bufferSize = bufferSize > 0 ? bufferSize : 100 * 1024;

            var maxFrameLength = FileFrameSplitter.ResolveMaxFrameLength();

            _options = new ChannelPipelineOptions
            {
                Splitter = FileFrameSplitter.Create(maxFrameLength),
                MaxBufferedBytes = (long)maxFrameLength + FrameConstants.P_Head,
                ReadBufferSize = _bufferSize,
                // 请求与文件块必须按发送顺序处理，同一连接串行执行。
                MaxConcurrentMessages = 1,
                RequireTerminalHandler = true
            };

            var option = new SocketOptionBuilder()
                .SetSocket()
                .UseStream()
                .UseConnectionOpened(OnConnectionOpened)
                .UseInputHandler(RunPipelineAsync)
                .SetIP(ip)
                .SetPort(port)
                .SetReadBufferSize(_bufferSize)
                .SetWriteBufferSize(_bufferSize)
                .Build();

            _client = SocketFactory.CreateClientSocket(option);

            _client.OnError += (id, ex) => LogHelper.Warn("FileSocket Client:" + id, ex);

            HeartAsync();
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
        /// 接管本连接的读循环：装配处理器链并运行到收口
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
                    _options,
                    token,
                    _host,
                    ConfigureConnection)
                    .ConfigureAwait(false);
            }
            finally
            {
                _pipeline = null;
            }
        }

        /// <summary>
        /// 装配本连接的处理器链：出站前缀 → 解码 → 应答分发 → 激活登记 → 链尾异常处理器
        /// </summary>
        /// <param name="channelId">连接标识</param>
        /// <param name="pipeline">尚未启动的处理器链</param>
        private void ConfigureConnection(string channelId, IChannelPipeline pipeline)
        {
            pipeline.AddLast(new FileMessageEncoder());
            pipeline.AddLast(new OutboundSerializingHandler());

            pipeline.AddLast(new FileMessageDecoder());
            pipeline.AddLast(new ResponseHandler(this));
            pipeline.AddLast(new ActiveHandler(this));
            pipeline.AddLast(new ExceptionTerminalHandler());
        }

        private void OnPipelineActive(IChannelPipeline pipeline)
        {
            _pipeline = pipeline;
            _pipelineReady.Set();
        }

        private void OnPipelineInactive()
        {
            _pipeline = null;
        }

        private void HeartAsync()
        {
            new Thread(new ThreadStart(() =>
            {
                _actived = DateTimeHelper.Now;
                try
                {
                    while (true)
                    {
                        if (_client.Connected)
                        {
                            if (_actived.AddMilliseconds(HeartSpan) <= DateTimeHelper.Now)
                            {
                                TrySend(new BaseSocketProtocal((byte)SocketProtocalType.Heart, ReadOnlyMemory<byte>.Empty));
                            }
                            Thread.Sleep(HeartSpan);
                        }
                        else
                        {
                            Thread.Sleep(1);
                        }
                    }
                }
                catch { }
            }))
            { IsBackground = true }.Start();
        }

        /// <summary>
        /// 发送传输请求，并把应答回调压入等待栈
        /// </summary>
        /// <param name="content">请求帧体</param>
        /// <param name="onComplete">收到应答时以是否允许接收为参数回调</param>
        private void SendRequest(byte[] content, Action<bool> onComplete)
        {
            // 先登记再发送，避免应答先于登记到达时无人接收。
            _eventCollection.Push(onComplete);

            TrySend(BaseSocketProtocal.ParseRequest(content));
        }

        /// <summary>
        /// 按块读取并发送文件；块内容在编码时同步复制，池化缓冲可在写出后立即复用
        /// </summary>
        /// <param name="fileName">待发送文件路径</param>
        /// <param name="offset">起始偏移（字节）</param>
        /// <returns>全部块写出完成后结束的任务</returns>
        private async Task SendFileCoreAsync(string fileName, long offset)
        {
            byte[] buffer = null;

            try
            {
                buffer = MemoryPoolManager.Rent(_bufferSize);

                using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    _total = fs.Length;

                    while (true)
                    {
                        fs.Position = offset;

                        var readNum = fs.Read(buffer, 0, _bufferSize);

                        offset += readNum;

                        if (readNum <= 0)
                            break;

                        var content = new ReadOnlyMemory<byte>(buffer, 0, readNum);

                        var protocal = new BaseSocketProtocal(
                            readNum,
                            (byte)SocketProtocalType.BigData,
                            content);

                        await SendProtocolAsync(protocal).ConfigureAwait(false);

                        Interlocked.Add(ref _out, readNum);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Warn("FileSocket 发送文件失败:" + fileName, ex);
            }
            finally
            {
                if (buffer != null)
                {
                    MemoryPoolManager.Return(buffer, _bufferSize);
                }
            }
        }

        /// <summary>
        /// 以「请求 → 允许 → 分块」的顺序发送一个文件
        /// </summary>
        /// <param name="fileName">待发送文件路径</param>
        /// <param name="offset">起始偏移（字节）</param>
        /// <param name="complete">发送流程结束时以是否允许接收为参数回调，可为 null</param>
        public void SendFile(string fileName, long offset, Action<bool> complete)
        {
            _autoResetEvent.WaitOne();

            if (!File.Exists(fileName))
                return;

            var fName = Path.GetFileName(fileName);

            var data = SerializeHelper.PBSerialize(new FileMessage() { FileName = fName, Length = new FileInfo(fileName).Length, Offset = offset });

            SendRequest(data, (d) =>
            {
                if (d)
                {
                    // 回调与信号量必须在整份文件写出后才放开，否则同一连接上的两轮文件块会交错。
                    _ = SendFileAndCompleteAsync(fileName, offset, complete);
                    return;
                }

                complete?.Invoke(false);
                _autoResetEvent.Set();
            });
        }

        /// <summary>
        /// 发送文件并在发送任务收尾后回调与放开下一个请求
        /// </summary>
        /// <param name="fileName">待发送文件路径</param>
        /// <param name="offset">起始偏移（字节）</param>
        /// <param name="complete">发送流程结束时以是否允许接收为参数回调，可为 null</param>
        /// <returns>回调与信号量释放完成后结束的任务</returns>
        private async Task SendFileAndCompleteAsync(string fileName, long offset, Action<bool> complete)
        {
            try
            {
                await SendFileCoreAsync(fileName, offset).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    complete?.Invoke(true);
                }
                finally
                {
                    // 回调抛异常也要放开信号量，否则后续发送会永久阻塞。
                    _autoResetEvent.Set();
                }
            }
        }

        /// <summary>
        /// 同步连接服务器，并等待处理器链接管该连接
        /// </summary>
        public void Connect()
        {
            _pipelineReady.Reset();

            _client.Connect();

            // 读循环由输入处理器在连接建立后接管；首次发送必须等链就绪，否则没有可用的写入口。
            _pipelineReady.Wait(PipelineReadyTimeout);
        }

        /// <summary>
        /// 断开与服务器的连接
        /// </summary>
        public void Disconnect()
        {
            _client.Disconnect();
        }

        /// <summary>
        /// 写出协议对象；链未就绪时抛 <see cref="ChannelClosedException"/>
        /// </summary>
        /// <param name="protocal">待写出的协议对象</param>
        /// <returns>写出完成或失败后结束的任务</returns>
        private Task SendProtocolAsync(BaseSocketProtocal protocal)
        {
            var pipeline = _pipeline;
            if (pipeline == null)
                throw new ChannelClosedException("连接尚未完成管线装配，不能发送。");

            return pipeline.WriteAsync(protocal);
        }

        /// <summary>
        /// 尽力写出协议对象：失败只记录，不向调用线程抛出
        /// </summary>
        /// <param name="protocal">待写出的协议对象</param>
        private void TrySend(BaseSocketProtocal protocal)
        {
            _ = TrySendCoreAsync(protocal);
        }

        /// <summary>
        /// 尽力写出的异步实现，失败只记录
        /// </summary>
        /// <param name="protocal">待写出的协议对象</param>
        /// <returns>写出完成或失败后结束的任务</returns>
        private async Task TrySendCoreAsync(BaseSocketProtocal protocal)
        {
            try
            {
                await SendProtocolAsync(protocal).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHelper.Debug("FileSocket 发送失败:" + ex.Message);
            }
        }

        /// <summary>
        /// 应答处理器：把允许或拒绝应答交回最近一次传输请求，并截断链尾
        /// </summary>
        private sealed class ResponseHandler : InboundHandlerAdapter
        {
            private readonly Client _owner;

            /// <summary>
            /// 绑定所属客户端
            /// </summary>
            /// <param name="owner">所属客户端实例</param>
            public ResponseHandler(Client owner)
            {
                _owner = owner;
            }

            /// <summary>
            /// 处理允许或拒绝应答；其它消息继续交给下游
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <param name="msg">已解帧的消息</param>
            /// <returns>处理完成后结束的任务</returns>
            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var protocal = msg as BaseSocketProtocal;
                if (protocal != null
                    && (protocal.Type == (byte)SocketProtocalType.AllowReceive
                        || protocal.Type == (byte)SocketProtocalType.RefuseReceive))
                {
                    Action<bool> action;
                    if (_owner._eventCollection.TryPop(out action) && action != null)
                    {
                        action(protocal.Type == (byte)SocketProtocalType.AllowReceive);
                    }
                    return;
                }

                await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 连接激活处理器：登记本连接的写入口，并在失活时清空
        /// </summary>
        private sealed class ActiveHandler : InboundHandlerAdapter
        {
            private readonly Client _owner;

            /// <summary>
            /// 绑定所属客户端
            /// </summary>
            /// <param name="owner">所属客户端实例</param>
            public ActiveHandler(Client owner)
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
                _owner.OnPipelineActive(ctx.Pipeline);
                return Task.CompletedTask;
            }

            /// <summary>
            /// 连接收口时清空写入口
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <returns>清理完成后结束的任务</returns>
            public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
            {
                _owner.OnPipelineInactive();
                return Task.CompletedTask;
            }
        }
    }
}