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
*命名空间：SAEA.QueueSocket
*文件名： QClient
*版本号： v26.4.23.1
*唯一标识：25a2f16c-e628-4aec-826f-d09936146044
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：QClient接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 20:10:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：改用流式处理器链收发队列帧，删除接收与生命周期事件，接收交给使用者处理器
*
*****************************************************************************/
using System;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Common.Threading;
using SAEA.QueueSocket.Model;
using SAEA.QueueSocket.Net;
using SAEA.QueueSocket.Type;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.QueueSocket
{
    /// <summary>
    /// 队列客户端类
    /// </summary>
    public class QClient
    {
        private DateTime Actived = DateTimeHelper.Now;

        private int HeartSpan;

        string _name;

        byte[] _nameBytes;

        bool _isClosed = false;

        private AutoResetEvent autoResetEvent = new AutoResetEvent(false);

        PooledBatcher _batcher;

        IClientSocket _clientSocket;

        /// <summary>
        /// 处理器链装配回调：每条连接在解码之后、异常收口之前调用一次，
        /// 使用者在此追加自己的入站处理器；为 null 时收到的消息由链尾回收。
        /// 赋值可在连接建立之前或运行期间进行，只影响其后建立的连接。
        /// </summary>
        public Action<string, IChannelPipeline> ConfigurePipeline { get; set; }

        readonly PipelineHostRegistry _host = new PipelineHostRegistry();

        readonly ChannelPipelineOptions _options;

        readonly int _timeOut = 3000;

        // 业务线程可能在连接已建立但链尚未激活的窗口里发起发送，这里等待链就绪后再写。
        readonly ManualResetEventSlim _pipelineReady = new ManualResetEventSlim(false);

        volatile IChannelPipeline _pipeline;

        /// <summary>
        /// 获取客户端是否已连接
        /// </summary>
        public bool Connected
        {
            get
            {
                return _clientSocket.Connected;
            }
        }

        /// <summary>
        /// 构造函数，初始化QClient实例
        /// </summary>
        /// <param name="name">客户端名称</param>
        /// <param name="ipPort">IP和端口</param>
        public QClient(string name, string ipPort) : this(name, 128 * 1024, ipPort.ToIPPort().Item1, ipPort.ToIPPort().Item2)
        {

        }

        /// <summary>
        /// 构造函数，初始化QClient实例
        /// </summary>
        /// <param name="name">客户端名称</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="ip">IP地址</param>
        /// <param name="port">端口号</param>
        public QClient(string name, int bufferSize = 128 * 1024, string ip = "127.0.0.1", int port = 39654)
        {
            QueueSocketThreadPool.EnsureConfigured();

            _name = name;

            _nameBytes = string.IsNullOrEmpty(name) ? null : Encoding.UTF8.GetBytes(name);

            HeartSpan = 60 * 1000;

            HeartAsync();

            _batcher = new PooledBatcher(1000, 50);

            _batcher.OnBatched += _batcher_OnBatched;

            var readBufferSize = bufferSize > 0 ? bufferSize : 128 * 1024;

            // 单帧上限沿用 BaseCoder.MaxFrameLength 的可配置口径，输入残余上限按同一口径显式给出。
            var maxFrameLength = QueueFrameSplitter.ResolveMaxFrameLength();

            _options = new ChannelPipelineOptions
            {
                Splitter = QueueFrameSplitter.Create(maxFrameLength),
                MaxBufferedBytes = (long)maxFrameLength + FrameConstants.P_Head,
                ReadBufferSize = readBufferSize,
                // 订阅消息必须按到达顺序交付，同一连接串行执行。
                MaxConcurrentMessages = 1
            };

            var socketOption = new SocketOptionBuilder()
                .SetSocket(SAEASocketType.Tcp)
                .UseStream()
                .UseConnectionOpened(OnConnectionOpened)
                .UseInputHandler(RunPipelineAsync)
                .SetIP(ip)
                .SetPort(port)
                .SetWriteBufferSize(readBufferSize)
                .SetReadBufferSize(readBufferSize)
                .ReusePort(false)
                .Build();

            _clientSocket = SocketFactory.CreateClientSocket(socketOption);

            _clientSocket.OnError += (id, ex) => LogHelper.Debug("QueueSocket 客户端异常:" + id + "," + ex.Message);
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
        /// 连接
        /// </summary>
        public void Connect()
        {
            _clientSocket.Connect();
        }

        /// <summary>
        /// 异步连接
        /// </summary>
        /// <param name="callBack">回调函数</param>
        public void ConnectAsync(Action<SocketError> callBack = null)
        {
            _clientSocket.ConnectAsync(callBack);
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            _clientSocket.Disconnect();
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
                (data, owner) => _clientSocket.SendAsync(data, owner));

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
                _pipelineReady.Reset();
            }
        }

        /// <summary>
        /// 装配本连接的处理器链：出站前缀 → 解码 → 激活登记 → 使用者扩展 → 链尾异常处理器
        /// </summary>
        /// <param name="channelId">连接标识</param>
        /// <param name="pipeline">尚未启动的处理器链</param>
        private void ConfigureConnection(string channelId, IChannelPipeline pipeline)
        {
            // 出站必须是链首连续前缀，串行器固定在最右，心跳与批量发布才不会乱序。
            pipeline.AddLast(new QueueMessageEncoder());
            pipeline.AddLast(new OutboundSerializingHandler());

            pipeline.AddLast(new QueueMessageDecoder());
            pipeline.AddLast(new ActiveHandler(this));

            var configure = ConfigurePipeline;
            if (configure != null)
                configure(channelId, pipeline);

            pipeline.AddLast(new ExceptionTerminalHandler());
        }

        /// <summary>
        /// 批量处理事件。writer 所有权随帧移交给处理器链，写完由传输层释放。
        /// </summary>
        /// <param name="writer">合并后的写入器</param>
        /// <param name="count">本批消息数量</param>
        private void _batcher_OnBatched(PooledBufferWriter writer, int count)
        {
            var pipeline = GetPipeline(_timeOut);
            if (pipeline == null)
            {
                writer.Dispose();
                return;
            }

            _ = WriteFrameAsync(pipeline, writer);
        }

        /// <summary>
        /// 取当前连接的处理器链，未就绪时在给定时间内等待链激活
        /// </summary>
        /// <param name="timeOut">等待毫秒数，小于等于 0 时不等待</param>
        /// <returns>可用的处理器链；超时仍不可用时为 null</returns>
        private IChannelPipeline GetPipeline(int timeOut)
        {
            var pipeline = _pipeline;
            if (pipeline != null)
                return pipeline;

            if (timeOut > 0)
                _pipelineReady.Wait(timeOut);

            return _pipeline;
        }

        /// <summary>
        /// 把已编码的整批帧交给处理器链
        /// </summary>
        /// <param name="pipeline">目标连接的处理器链</param>
        /// <param name="writer">已写入整批帧的写入器，所有权随帧移交</param>
        /// <returns>写出完成或失败后结束的任务</returns>
        private static async Task WriteFrameAsync(IChannelPipeline pipeline, PooledBufferWriter writer)
        {
            var frame = new RawFrame(writer.WrittenMemory, writer);
            try
            {
                await pipeline.WriteAsync(frame).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 未成功移交时由本方法归还背衬缓冲，已移交的帧由传输层负责。
                frame.Dispose();
                LogHelper.Debug("QueueSocket 批量发布失败:" + ex.Message);
            }
        }

        /// <summary>
        /// 把一条消息交给处理器链，链未就绪时短暂等待
        /// </summary>
        /// <param name="msg">待发送消息</param>
        /// <returns>已交给处理器链返回 true；等待超时返回 false</returns>
        private bool TrySend(QueueSocketMsg msg)
        {
            var pipeline = GetPipeline(_timeOut);
            if (pipeline == null)
                return false;

            _ = WriteCoreAsync(pipeline, msg);
            return true;
        }

        /// <summary>
        /// 写出消息并记录失败；写出是通知性质，不向业务线程抛出
        /// </summary>
        /// <param name="pipeline">目标连接的处理器链</param>
        /// <param name="msg">待发送消息</param>
        /// <returns>写出完成或失败后结束的任务</returns>
        private static async Task WriteCoreAsync(IChannelPipeline pipeline, object msg)
        {
            try
            {
                await pipeline.WriteAsync(msg).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 编码前的业务对象由创建方回收；这里只有无背衬缓冲的协议对象，交给 GC 即可。
                LogHelper.Debug("QueueSocket 写出失败:" + ex.Message);
            }
        }

        /// <summary>
        /// 心跳异步处理
        /// </summary>
        private void HeartAsync()
        {
            TaskHelper.LongRunning(() =>
            {
                try
                {
                    while (!_isClosed)
                    {
                        if (_clientSocket != null && _clientSocket.Connected)
                        {
                            if (Actived.AddMilliseconds(HeartSpan) <= DateTimeHelper.Now)
                            {
                                TrySend(new QueueSocketMsg(QueueSocketMsgType.Ping, _name));
                            }
                            autoResetEvent.WaitOne(HeartSpan / 2);
                        }
                        else
                        {
                            autoResetEvent.WaitOne(1000);
                        }
                    }
                }
                catch { }
            });
        }

        #region 生产者发送消息

        /// <summary>
        /// 生产者发送消息
        /// </summary>
        /// <param name="topic">主题</param>
        /// <param name="content">内容</param>
        public void Publish(string topic, string content)
        {
            var topicBytes = string.IsNullOrEmpty(topic) ? null : Encoding.UTF8.GetBytes(topic);
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var nameLen = _nameBytes == null ? 0 : _nameBytes.Length;
            var topicLen = topicBytes == null ? 0 : topicBytes.Length;
            var writer = new PooledBufferWriter(1 + 12 + nameLen + topicLen + contentBytes.Length);
            QueueMessageEncoder.WriteFrameTo(writer, QueueSocketMsgType.Publish, _nameBytes, topicBytes, contentBytes);
            if (!_batcher.Insert(writer))
            {
                writer.Dispose();
            }
        }

        #endregion

        #region 订阅者

        /// <summary>
        /// 订阅主题
        /// </summary>
        /// <param name="topic">主题</param>
        public void Subscribe(string topic)
        {
            TrySend(new QueueSocketMsg(QueueSocketMsgType.Subcribe, _name, topic));
        }

        /// <summary>
        /// 取消订阅主题
        /// </summary>
        /// <param name="topic">主题</param>
        public void Unsubscribe(string topic)
        {
            TrySend(new QueueSocketMsg(QueueSocketMsgType.Unsubcribe, _name, topic));
        }

        #endregion

        /// <summary>
        /// 关闭客户端
        /// </summary>
        /// <param name="wait">等待关闭帧交接完成的最长毫秒数</param>
        public void Close(int wait = 10000)
        {
            _isClosed = true;
            _batcher.OnBatched -= _batcher_OnBatched;
            _batcher?.Dispose();

            // 先让关闭帧交接到传输层再断开，否则对端可能读不到关闭通知。
            var pipeline = GetPipeline(_timeOut);
            if (pipeline != null)
            {
                try
                {
                    var sending = pipeline.WriteAsync(new QueueSocketMsg(QueueSocketMsgType.Close, _name));
                    if (wait > 0)
                        sending.Wait(wait);
                }
                catch
                {
                }
            }

            _clientSocket.Disconnect();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// 连接激活处理器：登记写入口并刷新最近活动时间，失活时清空写入口
        /// </summary>
        private sealed class ActiveHandler : InboundHandlerAdapter
        {
            private readonly QClient _owner;

            /// <summary>
            /// 绑定所属客户端
            /// </summary>
            /// <param name="owner">所属客户端实例</param>
            public ActiveHandler(QClient owner)
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
                _owner._pipelineReady.Set();
                return Task.CompletedTask;
            }

            /// <summary>
            /// 收到消息时刷新最近活动时间，再交给下游使用者处理器
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <param name="msg">已解码的队列消息</param>
            /// <returns>下游处理完成后结束的任务</returns>
            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                _owner.Actived = DateTimeHelper.Now;
                return ctx.FireChannelReadAsync(msg);
            }

            /// <summary>
            /// 连接收口时清空写入口
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <returns>清理完成后结束的任务</returns>
            public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
            {
                _owner._pipeline = null;
                _owner._pipelineReady.Reset();
                return Task.CompletedTask;
            }
        }
    }
}