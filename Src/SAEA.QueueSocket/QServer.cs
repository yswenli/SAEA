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
*文件名： QServer
*版本号： v26.4.23.1
*唯一标识：924b9dec-e30d-4373-9547-994ba4c60994
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/05/09 15:22:09
*描述：QServer服务端类
*
*=====================================================================
*修改标记
*修改时间：2026/11/16 10:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：通道链配置改为每连接新建，并显式开启启动期链尾校验
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
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
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.QueueSocket
{
    /// <summary>
    /// 队列服务器类
    /// </summary>
    public class QServer
    {
        Exchange _exchange;

        IServerSocket _serverSokcet;

        private readonly object _configurePipelineLock = new object();

        private Action<string, IChannelPipeline> _configurePipeline;

        private bool _configurePipelineFrozen;

        /// <summary>
        /// 处理器链装配回调：每条连接在解码器之后、内置分发器与异常收口之前调用一次，
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

        readonly PipelineHostRegistry _host = new PipelineHostRegistry();

        // 批量投递来自交换机的分发线程，不在收包回调里，需要按连接标识找回处理器链再写。
        readonly ConcurrentDictionary<string, IChannelPipeline> _pipelines =
            new ConcurrentDictionary<string, IChannelPipeline>(StringComparer.Ordinal);

        // 单帧上限与读缓冲大小在构造时解析一次，之后每条连接按同一口径新建分帧器与配置。
        readonly int _maxFrameLength;

        readonly int _readBufferSize;

        /// <summary>
        /// 创建一条连接的通道链配置：配置与分帧器实例不跨连接复用，
        /// 分帧器一旦引入连接级状态（如切换式分帧），共享实例会让多连接互相干扰
        /// </summary>
        /// <returns>尚未被任何链使用的独立配置实例</returns>
        private ChannelPipelineOptions CreateOptions()
        {
            return new ChannelPipelineOptions
            {
                Splitter = QueueFrameSplitter.Create(_maxFrameLength),
                MaxBufferedBytes = (long)_maxFrameLength + FrameConstants.P_Head,
                ReadBufferSize = _readBufferSize,
                // 订阅与发布必须按到达顺序处理，同一连接串行执行。
                MaxConcurrentMessages = 1,
                RequireTerminalHandler = true
            };
        }

        /// <summary>
        /// 初始化QServer类的新实例
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="ip">IP地址</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="maxConnects">最大连接数</param>
        /// <param name="maxPendingMsgCount">消息队列最大堆积数量，默认10000000</param>
        public QServer(int port = 39654, string ip = "127.0.0.1", int bufferSize = 128 * 1024, int maxConnects = 100, int maxPendingMsgCount = 10000000)
        {
            QueueSocketThreadPool.EnsureConfigured();

            _exchange = new Exchange(maxPendingMsgCount);

            _exchange.OnBatched += _exchange_OnBatched;

            var readBufferSize = bufferSize > 0 ? bufferSize : 128 * 1024;

            // 单帧上限沿用 BaseCoder.MaxFrameLength 的可配置口径，输入残余上限按同一口径显式给出。
            _maxFrameLength = QueueFrameSplitter.ResolveMaxFrameLength();

            _readBufferSize = readBufferSize;

            var config = new SocketOptionBuilder()
                .SetSocket(SAEASocketType.Tcp)
                .UseStream()
                .UseConnectionOpened(OnConnectionOpened)
                .UseInputHandler(RunPipelineAsync)
                .SetReadBufferSize(readBufferSize)
                .SetWriteBufferSize(readBufferSize)
                .SetMaxConnects(maxConnects)
                .SetIP(ip)
                .SetPort(port)
                .Build();

            _serverSokcet = SocketFactory.CreateServerSocket(config);
        }

        /// <summary>
        /// 连接门面登记钩子：队列链只依赖连接标识，不需要连接级信息，
        /// 这里仅满足「输入处理器必须与连接钩子成对配置」的约定。
        /// </summary>
        /// <param name="info">底层通道信息</param>
        private static void OnConnectionOpened(ChannelInfo info)
        {
        }

        /// <summary>
        /// 批量处理事件
        /// </summary>
        /// <param name="id">会话ID</param>
        /// <param name="writer">写入器</param>
        private void _exchange_OnBatched(string id, PooledBufferWriter writer)
        {
            IChannelPipeline pipeline;
            if (string.IsNullOrEmpty(id) || !_pipelines.TryGetValue(id, out pipeline))
            {
                writer.Dispose();
                return;
            }

            _ = SendBatchAsync(pipeline, id, writer);
        }

        /// <summary>
        /// 把一批已编码的订阅数据交给目标连接的处理器链，失败只记录不向分发线程抛出
        /// </summary>
        /// <param name="pipeline">目标连接的处理器链</param>
        /// <param name="id">会话ID</param>
        /// <param name="writer">已写入整批帧的写入器，所有权随帧移交</param>
        /// <returns>写出完成或失败后结束的任务</returns>
        private static async Task SendBatchAsync(IChannelPipeline pipeline, string id, PooledBufferWriter writer)
        {
            try
            {
                await pipeline.WriteAsync(new RawFrame(writer.WrittenMemory, writer)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHelper.Debug("QueueSocket 订阅数据写出失败:" + id + " " + ex.Message);
            }
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

            var stream = new PipelineRawByteStream(channelId, reader, flow, _serverSokcet);

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
        /// 装配单条连接的处理器链：出站前缀 → 解码 → 使用者扩展 → 内置分发 → 链尾异常处理器
        /// </summary>
        /// <param name="channelId">连接标识</param>
        /// <param name="pipeline">尚未启动的处理器链</param>
        private void ConfigureConnection(string channelId, IChannelPipeline pipeline)
        {
            // 出站必须是链首连续前缀，串行器固定在最右，批量投递与应答才不会乱序。
            pipeline.AddLast(new QueueMessageEncoder());
            pipeline.AddLast(new OutboundSerializingHandler());

            pipeline.AddLast(new QueueMessageDecoder());

            var configure = ConfigurePipeline;
            if (configure != null)
                configure(channelId, pipeline);

            pipeline.AddLast(new Dispatcher(this));
            pipeline.AddLast(new ExceptionTerminalHandler());

            // 链装配完成才登记：批量投递只允许写到结构完整的链上。
            _pipelines[channelId] = pipeline;
        }

        /// <summary>
        /// 回复消息
        /// </summary>
        /// <param name="ctx">当前连接在链上的节点上下文</param>
        /// <param name="queueResult">队列消息</param>
        /// <returns>应答与内置处理完成后结束的任务</returns>
        private async Task ReplyAsync(IChannelHandlerContext ctx, QueueMsg queueResult)
        {
            switch (queueResult.Type)
            {
                case QueueSocketMsgType.Ping:
                    await ReplyPongAsync(ctx, queueResult).ConfigureAwait(false);
                    break;
                case QueueSocketMsgType.Publish:
                    _exchange.AcceptPublish(ctx.ChannelId, queueResult);
                    break;
                case QueueSocketMsgType.Subcribe:
                    ReplySubcribe(ctx.ChannelId, queueResult);
                    break;
                case QueueSocketMsgType.Unsubcribe:
                    _exchange.Unsubscribe(ctx.ChannelId, queueResult);
                    break;
                case QueueSocketMsgType.Close:
                    await ReplyCloseAsync(ctx, queueResult).ConfigureAwait(false);
                    break;
            }
        }

        /// <summary>
        /// 回复Pong消息
        /// </summary>
        /// <param name="ctx">当前连接在链上的节点上下文</param>
        /// <param name="data">队列消息</param>
        /// <returns>写出完成后结束的任务</returns>
        private static Task ReplyPongAsync(IChannelHandlerContext ctx, QueueMsg data)
        {
            // 入站回调里不能借节点上下文向前转发，回包走整条链的业务写入口。
            return ctx.Pipeline.WriteAsync(new QueueSocketMsg(
                QueueSocketMsgType.Pong,
                data.Name,
                string.Empty,
                Encoding.UTF8.GetBytes(DateTimeHelper.ToString())));
        }

        /// <summary>
        /// 回复订阅消息
        /// </summary>
        /// <param name="channelId">连接标识</param>
        /// <param name="data">队列消息</param>
        private void ReplySubcribe(string channelId, QueueMsg data)
        {
            // 订阅登记只保存名称与主题，临时消息不持有数据，用完立即归还。
            var sInfo = new QueueMsg { Name = data.Name, Topic = data.Topic };
            try
            {
                _exchange.GetSubscribeData(channelId, sInfo);
            }
            finally
            {
                sInfo.Dispose();
            }
        }

        /// <summary>
        /// 回复关闭消息：先写回 Close 帧，再等已提交的写交接完成后收口
        /// </summary>
        /// <param name="ctx">当前连接在链上的节点上下文</param>
        /// <param name="data">队列消息</param>
        /// <returns>收口请求提交后结束的任务</returns>
        private async Task ReplyCloseAsync(IChannelHandlerContext ctx, QueueMsg data)
        {
            try
            {
                await ctx.Pipeline.WriteAsync(new QueueSocketMsg(QueueSocketMsgType.Close, data.Name)).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                // 对端常发完关闭帧立即断开，回包只是尽力而为，连接已收口时不再作为异常上报。
            }

            _exchange.Clear(ctx.ChannelId);
            await ctx.CloseAfterWriteAsync("queue-close").ConfigureAwait(false);
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        /// <param name="backlog">挂起连接队列的最大长度</param>
        public void Start(int backlog = 10 * 1000)
        {
            // 先冻结再监听：接受连接可能在 Start 返回前发生，冻结必须早于第一个连接建链。
            FreezeConfigurePipeline();

            _serverSokcet.Start(backlog);

            _running = true;
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public void Stop()
        {
            _running = false;

            _serverSokcet.Stop();

            _pipelines.Clear();

            if (_exchange != null)
            {
                _exchange.Dispose();
            }
        }

        /// <summary>
        /// 清除会话
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        public void Clear(string sessionID)
        {
            _exchange.Clear(sessionID);
        }

        bool _running = false;

        /// <summary>
        /// 获取服务器运行状态
        /// </summary>
        public bool Status => _running;

        /// <summary>
        /// 统计信息
        /// </summary>
        /// <param name="callBack">回调函数</param>
        public void CalcInfo(Action<Tuple<long, long, long, long>, List<Tuple<string, long>>> callBack)
        {
            TaskHelper.LongRunning(() =>
            {
                while (true)
                {
                    ThreadHelper.Sleep(1000);
                    if (_running)
                    {
                        var ci = _exchange.GetConnectInfo();
                        var qi = _exchange.GetQueueInfo();
                        callBack?.Invoke(ci, qi);                        
                    }
                }
            });
        }

        /// <summary>
        /// 内置分发处理器：把已解码的队列消息交给交换机，处理完即归还对象池；
        /// 连接收口时清理该会话的绑定与订阅
        /// </summary>
        private sealed class Dispatcher : InboundHandlerAdapter
        {
            private readonly QServer _owner;

            /// <summary>
            /// 绑定所属服务端
            /// </summary>
            /// <param name="owner">所属服务端实例</param>
            public Dispatcher(QServer owner)
            {
                _owner = owner;
            }

            /// <summary>
            /// 分发一条已解码的队列消息；非队列消息原样下传
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <param name="msg">已解码的消息</param>
            /// <returns>应答与内置处理完成后结束的任务</returns>
            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var queueMsg = msg as QueueMsg;
                if (queueMsg == null)
                {
                    await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
                    return;
                }

                try
                {
                    await _owner.ReplyAsync(ctx, queueMsg).ConfigureAwait(false);
                }
                finally
                {
                    // 接收路径上归还：发布消息的池化缓冲已由交换机摘除，这里只回收消息实例。
                    QueueMsgPool.Return(queueMsg);
                }
            }

            /// <summary>
            /// 连接收口时清理该会话的绑定与订阅
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <returns>清理完成后结束的任务</returns>
            public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
            {
                var sessionId = ctx.ChannelId;
                if (!string.IsNullOrEmpty(sessionId))
                    _owner._exchange.SessionClosed(sessionId);

                return Task.CompletedTask;
            }
        }
    }
}