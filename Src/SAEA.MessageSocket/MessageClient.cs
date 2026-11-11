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
*命名空间：SAEA.MessageSocket
*文件名： MessageClient
*版本号： v26.11.5.1
*唯一标识：1d6c1f66-9a54-4c47-9c1f-2c2a6c6b6f5d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：MessageClient客户端类
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 11:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：迁移到处理器链：删除连接/私信/频道/群组/异常五个事件，改由 ConfigurePipeline 装配，收发改走链路
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Common.Serialization;
using SAEA.Common.Threading;
using SAEA.MessageSocket.Model.Business;
using SAEA.MessageSocket.Model.Communication;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MessageSocket
{
    /// <summary>
    /// 消息客户端，提供登录、频道订阅、私聊与群组消息的发送和接收，构造后自动启动心跳
    /// </summary>
    public class MessageClient
    {
        /// <summary>
        /// 是否已收到服务端的登录成功应答
        /// </summary>
        public bool Logined
        {
            get; set;
        } = false;

        bool _subscribed = false;

        bool _unsubscribed = false;

        private int HeartSpan;

        IClientSocket _client;

        readonly PipelineHostRegistry _host = new PipelineHostRegistry();

        readonly ChannelPipelineOptions _options;

        readonly int _timeOut = 3000;

        // 业务线程可能在套接字已连接但链尚未激活的窗口里发起发送，这里等待链就绪后再写。
        readonly ManualResetEventSlim _pipelineReady = new ManualResetEventSlim(false);

        volatile IChannelPipeline _pipeline;

        DateTime _actived = DateTimeHelper.Now;

        /// <summary>
        /// 处理器链装配回调：每条连接在解码之后、异常收口之前调用一次，
        /// 使用者在此追加自己的入站处理器，收到的对象为已解码的消息；为 null 时只保留内置处理器。
        /// 赋值可在连接建立之前或运行期间进行，只影响其后建立的连接。
        /// </summary>
        public Action<string, IChannelPipeline> ConfigurePipeline { get; set; }

        /// <summary>
        /// 客户端标识，取底层连接的本地端点地址，与服务端记录该连接的标识一致；
        /// 连接建立前尚未绑定本地端口，返回 null
        /// </summary>
        public string ID
        {
            get
            {
                var socket = _client.Socket;

                var localEndPoint = socket == null ? null : socket.LocalEndPoint;

                return localEndPoint == null ? null : localEndPoint.ToString();
            }
        }

        /// <summary>
        /// 获取客户端是否已连接
        /// </summary>
        public bool Connected
        {
            get
            {
                return _client.Connected;
            }
        }

        /// <summary>
        /// 创建客户端，装配收发链路并启动心跳任务
        /// </summary>
        /// <param name="bufferSize">收发缓冲区大小（字节）</param>
        /// <param name="ip">服务器地址</param>
        /// <param name="port">服务器端口</param>
        public MessageClient(int bufferSize = 102400, string ip = "127.0.0.1", int port = 39654)
        {
            var readBufferSize = bufferSize > 0 ? bufferSize : 102400;

            // 单帧上限沿用 BaseCoder.MaxFrameLength 的可配置口径，输入残余上限按同一口径显式给出。
            var maxFrameLength = MessageFrameSplitter.ResolveMaxFrameLength();

            _options = new ChannelPipelineOptions
            {
                Splitter = MessageFrameSplitter.Create(maxFrameLength),
                MaxBufferedBytes = (long)maxFrameLength + FrameConstants.P_Head,
                ReadBufferSize = readBufferSize,
                // 应答必须先于其后的业务消息被处理，同一连接串行执行。
                MaxConcurrentMessages = 1
            };

            var option = new SocketOptionBuilder()
                .SetSocket(SAEASocketType.Tcp)
                .UseStream()
                .UseConnectionOpened(OnConnectionOpened)
                .UseInputHandler(RunPipelineAsync)
                .SetIP(ip)
                .SetPort(port)
                .SetReadBufferSize(readBufferSize)
                .SetWriteBufferSize(readBufferSize)
                .Build();

            _client = SocketFactory.CreateClientSocket(option);

            _client.OnError += (id, ex) => LogHelper.Debug("MessageSocket 客户端传输层异常:" + id + "," + ex.Message);

            HeartSpan = 30 * 1000;

            HeartAsync();
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
        /// 装配本连接的处理器链：出站前缀 → 解码 → 内置状态登记 → 激活登记 → 使用者扩展 → 链尾异常处理器
        /// </summary>
        /// <param name="channelId">连接标识</param>
        /// <param name="pipeline">尚未启动的处理器链</param>
        private void ConfigureConnection(string channelId, IChannelPipeline pipeline)
        {
            // 出站必须是链首连续前缀，串行器固定在最右，心跳与业务消息才不会乱序。
            pipeline.AddLast(new MessageEncoder());
            pipeline.AddLast(new OutboundSerializingHandler());

            pipeline.AddLast(new MessageDecoder());
            pipeline.AddLast(new InboundDispatcher(this));
            pipeline.AddLast(new ActiveHandler(this));

            var configure = ConfigurePipeline;
            if (configure != null)
                configure(channelId, pipeline);

            pipeline.AddLast(new ExceptionTerminalHandler());
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
        /// 把一条消息交给处理器链；链未就绪时在超时后丢弃
        /// </summary>
        /// <param name="chatMessage">待发送的消息</param>
        private void Send(ChatMessage chatMessage)
        {
            var pipeline = GetPipeline(_timeOut);
            if (pipeline == null)
            {
                LogHelper.Debug("MessageSocket 客户端尚未就绪，消息被丢弃:" + chatMessage.Type);
                return;
            }

            _actived = DateTimeHelper.Now;

            _ = WriteCoreAsync(pipeline, chatMessage);
        }

        /// <summary>
        /// 把一条消息交给处理器链并等待交接完成；链未就绪时在超时后静默丢弃
        /// </summary>
        /// <param name="chatMessage">待发送的消息</param>
        /// <param name="timeoutMs">等待链就绪的毫秒数，小于等于 0 时不等待</param>
        /// <returns>写出完成或失败后结束的任务</returns>
        private async Task SendAsync(ChatMessage chatMessage, int timeoutMs)
        {
            var pipeline = GetPipeline(timeoutMs);
            if (pipeline == null)
            {
                LogHelper.Debug("MessageSocket 客户端尚未就绪，消息被丢弃:" + chatMessage.Type);
                return;
            }

            _actived = DateTimeHelper.Now;

            try
            {
                await pipeline.WriteAsync(chatMessage).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHelper.Debug("MessageSocket 发送失败:" + chatMessage.Type + "," + ex.Message);
            }
        }

        /// <summary>
        /// 写出消息并记录失败；写出是通知性质，不向业务线程抛出
        /// </summary>
        /// <param name="pipeline">目标连接的处理器链</param>
        /// <param name="chatMessage">待发送的消息</param>
        /// <returns>写出完成或失败后结束的任务</returns>
        private static async Task WriteCoreAsync(IChannelPipeline pipeline, ChatMessage chatMessage)
        {
            try
            {
                await pipeline.WriteAsync(chatMessage).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHelper.Debug("MessageSocket 发送失败:" + chatMessage.Type + "," + ex.Message);
            }
        }

        /// <summary>
        /// 心跳异步处理：空闲超过心跳间隔时向服务端发送一个空帧
        /// </summary>
        void HeartAsync()
        {
            TaskHelper.LongRunning(() =>
            {
                try
                {
                    while (true)
                    {
                        if (_client.Connected)
                        {
                            ThreadHelper.Sleep(1000);

                            if (_actived.AddMilliseconds(HeartSpan) <= DateTimeHelper.Now)
                                _ = WriteHeartAsync();
                        }
                        else
                        {
                            ThreadHelper.Sleep(1000);
                        }
                    }
                }
                catch { }
            });
        }

        /// <summary>
        /// 写出一个心跳帧；帧已是完整线格式，故直接交给链路而不再编码
        /// </summary>
        /// <returns>写出完成或失败后结束的任务</returns>
        private async Task WriteHeartAsync()
        {
            var pipeline = _pipeline;
            if (pipeline == null)
                return;

            var writer = new PooledBufferWriter(FrameConstants.P_Head);

            RawFrame frame = null;

            try
            {
                new BaseSocketProtocal((byte)SocketProtocalType.Heart, ReadOnlyMemory<byte>.Empty).WriteTo(writer);

                frame = new RawFrame(writer.WrittenMemory, writer);

                await pipeline.WriteAsync(frame).ConfigureAwait(false);
            }
            catch
            {
                // 未成功移交时由本方法归还背衬缓冲；已移交的帧由传输层负责。
                if (frame != null)
                    frame.Dispose();
                else
                    writer.Dispose();
            }
        }

        /// <summary>
        /// 异步发起连接，连接建立后由链路激活回调通知处理器
        /// </summary>
        public void ConnectAsync()
        {
            if (!_client.Connected)
            {
                _client.ConnectAsync((c) =>
                {
                    if (c != System.Net.Sockets.SocketError.Success)
                    {
                        throw new KernelException("连接到消息服务器失败，Code:" + c.ToString());
                    }
                });
            }
        }

        /// <summary>
        /// 同步连接服务器，连接建立后由链路激活回调通知处理器
        /// </summary>
        public void Connect()
        {
            if (!_client.Connected)
            {
                _client.Connect();
            }
        }

        /// <summary>
        /// 发送登录请求，收到服务端应答后 Logined 才置为 true
        /// </summary>
        public void Login()
        {
            Send(new ChatMessage(ChatMessageType.Login, ""));
        }

        /// <summary>
        /// 发送频道订阅请求并返回本地记录的订阅状态，应答异步到达，返回值可能仍是上次的结果
        /// </summary>
        /// <param name="name">频道名称</param>
        /// <returns>本地记录的订阅状态，不代表本次请求的处理结果</returns>
        public bool Subscribe(string name)
        {
            Send(new ChatMessage(ChatMessageType.Subscribe, name));
            return _subscribed;
        }

        /// <summary>
        /// 发送取消订阅请求并返回本地记录的退订状态，应答异步到达，返回值可能仍是上次的结果
        /// </summary>
        /// <param name="name">频道名称</param>
        /// <returns>本地记录的退订状态，不代表本次请求的处理结果</returns>
        public bool Unsubscribe(string name)
        {
            Send(new ChatMessage(ChatMessageType.UnSubscribe, name));
            return _unsubscribed;
        }

        /// <summary>
        /// 发送频道消息，由服务端填入发送者与时间后转发给同频道其他成员
        /// </summary>
        /// <param name="channelName">频道名称</param>
        /// <param name="content">消息正文</param>
        public void SendChannelMsg(string channelName, string content)
        {
            ChannelMessage cm = new ChannelMessage()
            {
                Name = channelName,
                Content = content
            };

            Send(new ChatMessage(ChatMessageType.ChannelMessage, cm));
        }

        /// <summary>
        /// 发送私聊消息，链未就绪或写出失败时静默丢弃
        /// </summary>
        /// <param name="receiver">接收者标识</param>
        /// <param name="content">消息正文</param>
        public void SendPrivateMsg(string receiver, string content)
        {
            PrivateMessage pm = new PrivateMessage()
            {
                Receiver = receiver,
                Content = content
            };

            Send(new ChatMessage(ChatMessageType.PrivateMessage, pm));
        }

        /// <summary>
        /// 异步发送私聊消息，在超时时间内等待链就绪，超时后静默丢弃本条消息
        /// </summary>
        /// <param name="receiver">接收者标识</param>
        /// <param name="content">消息正文</param>
        /// <param name="timeoutMs">等待链就绪的超时时间（毫秒）</param>
        /// <returns>写出完成或失败后结束的任务</returns>
        public Task SendPrivateMsgAsync(string receiver, string content, int timeoutMs = 5000)
        {
            PrivateMessage pm = new PrivateMessage()
            {
                Receiver = receiver,
                Content = content
            };

            return SendAsync(new ChatMessage(ChatMessageType.PrivateMessage, pm), timeoutMs);
        }

        #region group

        /// <summary>
        /// 发送创建群组请求，创建成功后发起者自动成为群成员，群名重复时服务端应答失败
        /// </summary>
        /// <param name="groupName">群组名称</param>
        public void SendCreateGroup(string groupName)
        {
            Send(new ChatMessage(ChatMessageType.CreateGroup, groupName));
        }

        /// <summary>
        /// 发送解散群组请求，服务端仅按群名删除且不校验发起者是否为群主
        /// </summary>
        /// <param name="groupName">群组名称</param>
        public void SendRemoveGroup(string groupName)
        {
            Send(new ChatMessage(ChatMessageType.RemoveGroup, groupName));
        }

        /// <summary>
        /// 发送加入群组请求，把当前用户加入指定群组，已在群内时服务端应答失败
        /// </summary>
        /// <param name="groupName">群组名称</param>
        public void SendAddMember(string groupName)
        {
            Send(new ChatMessage(ChatMessageType.AddMember, groupName));
        }

        /// <summary>
        /// 发送退出群组请求，普通成员只移除自己，群主退出时服务端直接解散该群组
        /// </summary>
        /// <param name="groupName">群组名称</param>
        public void SendRemoveMember(string groupName)
        {
            Send(new ChatMessage(ChatMessageType.RemoveMember, groupName));
        }

        /// <summary>
        /// 发送群组消息，由服务端填入发送者与时间后转发给群内其他成员
        /// </summary>
        /// <param name="groupName">群组名称</param>
        /// <param name="content">消息正文</param>
        public void SendGroupMessage(string groupName, string content)
        {
            GroupMessage pm = new GroupMessage()
            {
                Name = groupName,
                Content = content
            };

            Send(new ChatMessage(ChatMessageType.GroupMessage, pm));
        }

        #endregion

        /// <summary>
        /// 入站分发处理器：登记登录与订阅状态，并把消息原样传给下游使用者处理器。
        /// </summary>
        private sealed class InboundDispatcher : InboundHandlerAdapter
        {
            private readonly MessageClient _owner;

            /// <summary>
            /// 绑定所属客户端
            /// </summary>
            /// <param name="owner">所属客户端实例</param>
            public InboundDispatcher(MessageClient owner)
            {
                _owner = owner;
            }

            /// <summary>
            /// 登记内置状态后把消息原样下传；非消息对象直接下传
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <param name="msg">已解码的消息</param>
            /// <returns>下游处理完成后结束的任务</returns>
            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var chatMessage = msg as ChatMessage;

                if (chatMessage != null)
                {
                    switch (chatMessage.Type)
                    {
                        case ChatMessageType.LoginAnswer:
                            _owner.Logined = true;
                            break;
                        case ChatMessageType.SubscribeAnswer:
                            _owner._subscribed = chatMessage.Content == "1";
                            break;
                        case ChatMessageType.UnSubscribeAnswer:
                            _owner._unsubscribed = chatMessage.Content == "1";
                            break;
                    }
                }

                return ctx.FireChannelReadAsync(msg);
            }
        }

        /// <summary>
        /// 连接激活处理器：登记写入口并刷新最近活动时间，失活时清空写入口
        /// </summary>
        private sealed class ActiveHandler : InboundHandlerAdapter
        {
            private readonly MessageClient _owner;

            /// <summary>
            /// 绑定所属客户端
            /// </summary>
            /// <param name="owner">所属客户端实例</param>
            public ActiveHandler(MessageClient owner)
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
                _owner._actived = DateTimeHelper.Now;

                _owner._pipeline = ctx.Pipeline;
                _owner._pipelineReady.Set();

                return Task.CompletedTask;
            }

            /// <summary>
            /// 收到消息时刷新最近活动时间
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <param name="msg">已解码的消息</param>
            /// <returns>下游处理完成后结束的任务</returns>
            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                _owner._actived = DateTimeHelper.Now;
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