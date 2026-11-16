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
*文件名： RClient
*版本号： v26.4.23.1
*唯一标识：4eb3fffd-db26-4688-aeed-5770e2d950f2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/05/25 17:28:26
*描述：RClient接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/16 10:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：通道链配置显式开启启动期链尾校验
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.Common.Serialization;
using SAEA.Common.Threading;
using SAEA.RPC.Common;
using SAEA.RPC.Model;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.RPC.Net
{
    internal class RClient : ISyncBase, IDisposable
    {
        bool _isDisposed = false;

        readonly DisorderSyncHelper _disorderSyncHelper;

        readonly IClientSocket _client;

        readonly PipelineHostRegistry _host = new PipelineHostRegistry();

        readonly ChannelPipelineOptions _options;

        readonly int _timeOut = 3000;

        // 连接建立（处理器链激活）后才允许发送：业务线程可能在 Socket 已连接但链未激活的窗口里发起调用。
        readonly ManualResetEventSlim _pipelineReady = new ManualResetEventSlim(false);

        volatile IChannelPipeline _pipeline;

        public object SyncLocker { get; } = new object();

        public bool Connected
        {
            get
            {
                return _client == null ? false : _client.Connected;
            }
        }

        /// <summary>
        /// 收到服务端通知时调用，参数为反序列化前的数据
        /// </summary>
        public Action<byte[]> NoticeHandler { get; set; }

        /// <summary>
        /// 连接收口时调用，第二个参数固定为 null（断开不是异常）
        /// </summary>
        public Action<string, Exception> DisconnectedHandler { get; set; }

        /// <summary>
        /// 接收或发送出现异常时调用
        /// </summary>
        public Action<string, Exception> ErrorHandler { get; set; }

        private readonly object _configurePipelineLock = new object();

        private Action<string, IChannelPipeline> _configurePipeline;

        private bool _configurePipelineFrozen;

        /// <summary>
        /// 处理器链装配回调：每条连接在解码之后、异常收口之前调用一次，
        /// 使用者在此追加自己的入站处理器；为 null 时只保留内置处理器。
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

        public RClient(Uri uri, int timeOut = 3000)
        {
            if (string.IsNullOrEmpty(uri.Scheme) || string.Compare(uri.Scheme, "rpc", true) != 0)
            {
                ExceptionCollector.Add("Consumer.RClient.Init Error", new RPCSocketException("当前连接协议不正确，请使用格式rpc://ip:port"));
                return;
            }

            _timeOut = timeOut;

            _disorderSyncHelper = new DisorderSyncHelper(_timeOut);

            var ipPort = DNSHelper.GetIPPort(uri);

            // 单帧上限沿用 BaseCoder.MaxFrameLength 的可配置口径，输入残余上限按同一口径显式给出。
            var maxFrameLength = RpcFrameSplitter.ResolveMaxFrameLength();

            _options = new ChannelPipelineOptions
            {
                Splitter = RpcFrameSplitter.Create(maxFrameLength),
                MaxBufferedBytes = (long)maxFrameLength + FrameConstants.P_Head,
                ReadBufferSize = 10240,
                // 请求与应答必须按到达顺序处理，同一连接串行执行。
                MaxConcurrentMessages = 1,
                RequireTerminalHandler = true
            };

            var option = new SocketOptionBuilder()
                .SetSocket()
                .UseStream()
                .UseConnectionOpened(OnConnectionOpened)
                .UseInputHandler(RunPipelineAsync)
                .SetIP(ipPort.Item1)
                .SetPort(ipPort.Item2)
                .SetReadBufferSize(10240)
                .SetWriteBufferSize(10240)
                .SetActionTimeOut(_timeOut)
                .Build();

            _client = SocketFactory.CreateClientSocket(option);

            _client.OnError += (id, ex) =>
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
                _pipelineReady.Reset();
            }
        }

        /// <summary>
        /// 装配本连接的处理器链：出站前缀 → 解码 → 应答分发 → 激活登记 → 使用者扩展 → 链尾异常处理器
        /// </summary>
        /// <param name="channelId">连接标识</param>
        /// <param name="pipeline">尚未启动的处理器链</param>
        private void ConfigureConnection(string channelId, IChannelPipeline pipeline)
        {
            // 出站必须是链首连续前缀，串行器固定在最右，请求与心跳才不会乱序。
            pipeline.AddLast(new RpcMessageEncoder());
            pipeline.AddLast(new OutboundSerializingHandler());

            pipeline.AddLast(new RpcMessageDecoder());
            pipeline.AddLast(new InboundDispatcher(this));
            pipeline.AddLast(new ActiveHandler(this));

            var configure = ConfigurePipeline;
            if (configure != null)
                configure(channelId, pipeline);

            pipeline.AddLast(new ExceptionTerminalHandler());
        }

        /// <summary>
        /// 连接
        /// </summary>
        public void Connect()
        {
            // 首次连接即冻结装配回调：后续重连沿用同一份配置，避免已建链的连接与回调不一致。
            FreezeConfigurePipeline();

            _client.ConnectAsync();
        }

        /// <summary>
        /// 连接
        /// </summary>
        /// <returns></returns>
        public async Task ConnectAsync()
        {
            await Task.Yield();

            Connect();
        }

        /// <summary>
        /// 发送心跳
        /// </summary>
        internal void KeepAlive()
        {
            TaskHelper.LongRunning(() =>
            {
                while (!_isDisposed)
                {
                    try
                    {
                        if (_client.Connected)
                        {
                            if (_client.Context.UserToken.Actived.AddSeconds(60) < DateTimeHelper.Now)
                            {
                                // 心跳允许在链尚未激活时丢弃，不阻塞也不报错。
                                TrySend(new RSocketMsg(RSocketMsgType.Ping));
                            }
                        }
                        ThreadHelper.Sleep(1000);
                    }
                    catch (Exception ex)
                    {
                        ExceptionCollector.Add("Consumer.KeepAlive Error", ex);
                    }
                }
            });
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="msg"></param>
        internal void SendBase(RSocketMsg msg)
        {
            // 调用方在 Socket 已连接但链未激活时发起请求，这里按超时窗口等一次激活，避免必然超时。
            if (_pipeline == null)
                _pipelineReady.Wait(_timeOut);

            var pipeline = _pipeline;
            if (pipeline == null)
                throw new RPCSocketException("RPC 连接尚未就绪，无法发送。");

            _ = WriteCoreAsync(pipeline, msg);
        }

        /// <summary>
        /// 尽力发送一条消息，不等待连接就绪，也不抛异常
        /// </summary>
        /// <param name="msg">待发送消息</param>
        /// <returns>已交给处理器链返回 true，链路未就绪返回 false</returns>
        private bool TrySend(RSocketMsg msg)
        {
            var pipeline = _pipeline;
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
        private static async Task WriteCoreAsync(IChannelPipeline pipeline, RSocketMsg msg)
        {
            try
            {
                await pipeline.WriteAsync(msg).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ExceptionCollector.Add("Consumer.Send.Error", ex);
            }
        }

        /// <summary>
        /// 发送请求
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="method"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public byte[] Request(string serviceName, string method, byte[] args)
        {
            byte[] result = null;

            try
            {
                var msg = new RSocketMsg(RSocketMsgType.Request, serviceName, method)
                {
                    SequenceNumber = UniqueKeyHelper.Next()
                };

                msg.Data = args;

                result = _disorderSyncHelper.Wait(msg.SequenceNumber, () => { SendBase(msg); });
            }
            catch (Exception ex)
            {
                ExceptionCollector.Add("Consumer.Request.Error", new RPCSocketException($"serviceName:{serviceName}/method:{method} 调用超时！", ex));
            }
            return result;
        }

        /// <summary>
        /// 向rpc service provider 注册接收通知
        /// </summary>
        public void RegistReceiveNotice()
        {
            var msg = new RSocketMsg(RSocketMsgType.RegistNotice, null, null)
            {
                SequenceNumber = UniqueKeyHelper.Next()
            };
            SendBase(msg);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _isDisposed = true;
            _pipeline = null;
            _client.Dispose();
        }

        /// <summary>
        /// 连接激活处理器：登记本连接的写入口，并在失活时清空
        /// </summary>
        private sealed class ActiveHandler : InboundHandlerAdapter
        {
            private readonly RClient _owner;

            /// <summary>
            /// 绑定所属客户端
            /// </summary>
            /// <param name="owner">所属客户端实例</param>
            public ActiveHandler(RClient owner)
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
            /// 连接收口时清空写入口并通知上层
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <returns>清理完成后结束的任务</returns>
            public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
            {
                _owner._pipeline = null;
                _owner._pipelineReady.Reset();

                var handler = _owner.DisconnectedHandler;
                if (handler != null)
                    handler(_owner._client.Endpoint, null);

                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// 入站分发处理器：匹配请求应答、处理通知与协议错误，然后把消息原样传给下游使用者处理器
        /// </summary>
        private sealed class InboundDispatcher : InboundHandlerAdapter
        {
            private readonly RClient _owner;

            /// <summary>
            /// 绑定所属客户端
            /// </summary>
            /// <param name="owner">所属客户端实例</param>
            public InboundDispatcher(RClient owner)
            {
                _owner = owner;
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

                try
                {
                    switch ((RSocketMsgType)rpcMsg.Type)
                    {
                        case RSocketMsgType.Ping:
                            break;
                        case RSocketMsgType.Pong:

                            break;
                        case RSocketMsgType.Request:
                            break;
                        case RSocketMsgType.Response:
                            _owner._disorderSyncHelper.Set(rpcMsg.SequenceNumber, rpcMsg.Data);
                            break;
                        case RSocketMsgType.Notice:
                            var noticed = _owner.NoticeHandler;
                            if (noticed != null)
                                noticed(rpcMsg.Data);
                            break;
                        case RSocketMsgType.Error:
                            ExceptionCollector.Add("Consumer.OnReceived Error", new Exception(SAEASerialize.Deserialize<string>(rpcMsg.Data)));
                            _owner._disorderSyncHelper.Set(rpcMsg.SequenceNumber, rpcMsg.Data);
                            break;
                        case RSocketMsgType.Close:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    var handler = _owner.ErrorHandler;
                    if (handler != null)
                        handler(_owner._client.Endpoint, ex);
                }

                await ctx.FireChannelReadAsync(rpcMsg).ConfigureAwait(false);
            }
        }
    }
}