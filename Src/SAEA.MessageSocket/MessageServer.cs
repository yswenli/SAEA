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
*文件名： MessageServer
*版本号： v26.11.5.1
*唯一标识：b2dfc24c-0813-4150-8fe6-d2575be77aeb
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：MessageServer服务端类
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 11:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：迁移到处理器链：删除接入/断开/异常三个事件，改由 ConfigurePipeline 装配，收发改走链路
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Serialization;
using SAEA.MessageSocket.Collection;
using SAEA.MessageSocket.Model.Business;
using SAEA.MessageSocket.Model.Communication;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MessageSocket
{
    /// <summary>
    /// 消息服务端，处理登录、频道订阅与群组管理，并在成员之间转发消息
    /// </summary>
    public class MessageServer
    {
        IServerSocket _server;

        ChannelList _channelList = new ChannelList();

        GroupList _groupList = new GroupList();

        readonly PipelineHostRegistry _host = new PipelineHostRegistry();

        // 转发与应答都要按连接标识找回处理器链：连接级写入口只存在于链上。
        readonly ConcurrentDictionary<string, IChannelPipeline> _pipelines =
            new ConcurrentDictionary<string, IChannelPipeline>(StringComparer.Ordinal);

        readonly ChannelPipelineOptions _options;

        /// <summary>
        /// 处理器链装配回调：每条连接在解码之后、异常收口之前调用一次，
        /// 使用者在此追加自己的入站处理器；为 null 时只保留内置处理器。
        /// 赋值可在 Start 之前或运行期间进行，只影响其后建立的连接。
        /// </summary>
        public Action<string, IChannelPipeline> ConfigurePipeline { get; set; }

        /// <summary>
        /// 当前在线客户端数量
        /// </summary>
        public int ClientCounts
        {
            get
            {
                return _server.ClientCounts;
            }
        }

        /// <summary>
        /// 创建服务端，装配收发链路并准备频道与群组管理
        /// </summary>
        /// <param name="port">监听端口</param>
        /// <param name="bufferSize">收发缓冲区大小（字节）</param>
        /// <param name="count">最大连接数</param>
        /// <param name="timeOut">会话空闲超时时间（毫秒）</param>
        public MessageServer(int port = 39654, int bufferSize = 102400, int count = 1000, int timeOut = 60 * 1000)
        {
            var readBufferSize = bufferSize > 0 ? bufferSize : 102400;

            // 单帧上限沿用 BaseCoder.MaxFrameLength 的可配置口径，输入残余上限按同一口径显式给出。
            var maxFrameLength = MessageFrameSplitter.ResolveMaxFrameLength();

            _options = new ChannelPipelineOptions
            {
                Splitter = MessageFrameSplitter.Create(maxFrameLength),
                MaxBufferedBytes = (long)maxFrameLength + FrameConstants.P_Head,
                ReadBufferSize = readBufferSize,
                // 同一会话的请求必须按到达顺序处理：登录、订阅与转发都依赖先后关系。
                MaxConcurrentMessages = 1
            };

            var option = new SocketOptionBuilder()
                .SetSocket(SAEASocketType.Tcp)
                .UseStream()
                .UseConnectionOpened(OnConnectionOpened)
                .UseInputHandler(RunPipelineAsync)
                .SetPort(port)
                .SetReadBufferSize(readBufferSize)
                .SetWriteBufferSize(readBufferSize)
                .SetMaxConnects(count)
                .SetActionTimeOut(timeOut)
                .Build();

            _server = SocketFactory.CreateServerSocket(option);

            _server.OnError += (id, ex) => LogHelper.Error("MessageServer 传输层异常:" + id, ex);
        }

        /// <summary>
        /// 连接门面登记钩子：处理器链只依赖连接标识，这里仅满足
        /// 「输入处理器必须与连接钩子成对配置」的约定。
        /// </summary>
        /// <param name="info">底层通道信息</param>
        private static void OnConnectionOpened(SAEA.Sockets.Model.ChannelInfo info)
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
                    _options,
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
        /// 装配单条连接的处理器链：出站前缀 → 解码 → 内置分发 → 使用者扩展 → 链尾异常处理器
        /// </summary>
        /// <param name="channelId">连接标识</param>
        /// <param name="pipeline">尚未启动的处理器链</param>
        private void ConfigureConnection(string channelId, IChannelPipeline pipeline)
        {
            // 出站必须是链首连续前缀，串行器固定在最右，应答与转发才不会乱序。
            pipeline.AddLast(new MessageEncoder());
            pipeline.AddLast(new OutboundSerializingHandler());

            pipeline.AddLast(new MessageDecoder());
            pipeline.AddLast(new InboundDispatcher(this));

            var configure = ConfigurePipeline;
            if (configure != null)
                configure(channelId, pipeline);

            pipeline.AddLast(new ExceptionTerminalHandler());

            // 链装配完成才登记：转发只允许写到结构完整的链上。
            _pipelines[channelId] = pipeline;
        }

        /// <summary>
        /// 启动服务端并开始监听
        /// </summary>
        public void Start()
        {
            _server.Start();
        }

        /// <summary>
        /// 停止服务端并关闭所有连接
        /// </summary>
        public void Stop()
        {
            _server.Stop();

            _pipelines.Clear();
        }

        #region reply

        /// <summary>
        /// 处理一条已解码的消息
        /// </summary>
        /// <param name="ctx">当前连接在链上的节点上下文</param>
        /// <param name="chatMessage">已解码的消息</param>
        /// <returns>内置处理与应答写出完成后结束的任务</returns>
        private Task DispatchAsync(IChannelHandlerContext ctx, ChatMessage chatMessage)
        {
            switch (chatMessage.Type)
            {
                case ChatMessageType.Login:
                    return ReplyLoginAsync(ctx, chatMessage);
                case ChatMessageType.Subscribe:
                    return ReplySubscribeAsync(ctx, chatMessage);
                case ChatMessageType.UnSubscribe:
                    return ReplyUnsubscribeAsync(ctx, chatMessage);
                case ChatMessageType.ChannelMessage:
                    return ReplyChannelMessageAsync(ctx, chatMessage);
                case ChatMessageType.PrivateMessage:
                    return ReplyPrivateMessageAsync(ctx, chatMessage);
                case ChatMessageType.CreateGroup:
                    return ReplyCreateGroupAsync(ctx, chatMessage);
                case ChatMessageType.AddMember:
                    return ReplyAddMemberAsync(ctx, chatMessage);
                case ChatMessageType.RemoveMember:
                    return ReplyRemoveMemberAsync(ctx, chatMessage);
                case ChatMessageType.RemoveGroup:
                    return ReplyRemoveGroupAsync(ctx, chatMessage);
                case ChatMessageType.GroupMessage:
                    return ReplyGroupMessageAsync(ctx, chatMessage);
                default:
                    throw new InvalidOperationException("未知的消息协议:" + chatMessage.Type);
            }
        }

        /// <summary>
        /// 向当前连接回写一条消息
        /// </summary>
        /// <param name="ctx">当前连接在链上的节点上下文</param>
        /// <param name="chatMessage">待回写的消息</param>
        /// <returns>写出完成或失败后结束的任务</returns>
        private static async Task ReplyAsync(IChannelHandlerContext ctx, ChatMessage chatMessage)
        {
            // 入站回调里不能借节点上下文向前转发，回包走整条链的业务写入口。
            try
            {
                await ctx.Pipeline.WriteAsync(chatMessage).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 应答失败通常是对端已断开，属于尽力而为，记日志即可。
                LogHelper.Warn("MessageServer 应答写出失败:" + ctx.ChannelId, ex);
            }
        }

        /// <summary>
        /// 向指定连接转发一条消息
        /// </summary>
        /// <param name="channelId">目标连接标识</param>
        /// <param name="chatMessage">待转发的消息</param>
        /// <returns>写出完成或失败后结束的任务</returns>
        private async Task ForwardAsync(string channelId, ChatMessage chatMessage)
        {
            if (string.IsNullOrEmpty(channelId))
                return;

            IChannelPipeline pipeline;
            if (!_pipelines.TryGetValue(channelId, out pipeline))
                return;

            try
            {
                await pipeline.WriteAsync(chatMessage).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 目标连接可能刚好收口，转发是尽力而为，记日志即可。
                LogHelper.Warn("MessageServer 转发失败:" + channelId, ex);
            }
        }

        /// <summary>
        /// 回复登录请求：登录成功不携带额外内容，客户端据此置位登录状态
        /// </summary>
        /// <param name="ctx">当前连接在链上的节点上下文</param>
        /// <param name="chatMessage">登录请求</param>
        /// <returns>应答写出完成后结束的任务</returns>
        private Task ReplyLoginAsync(IChannelHandlerContext ctx, ChatMessage chatMessage)
        {
            return ReplyAsync(ctx, new ChatMessage(ChatMessageType.LoginAnswer, ""));
        }

        /// <summary>
        /// 回复频道订阅请求
        /// </summary>
        /// <param name="ctx">当前连接在链上的节点上下文</param>
        /// <param name="chatMessage">订阅请求，内容为频道名</param>
        /// <returns>应答写出完成后结束的任务</returns>
        private Task ReplySubscribeAsync(IChannelHandlerContext ctx, ChatMessage chatMessage)
        {
            var result = _channelList.Subscribe(chatMessage.Content, ctx.ChannelId) ? "1" : "0";

            return ReplyAsync(ctx, new ChatMessage(ChatMessageType.SubscribeAnswer, result));
        }

        /// <summary>
        /// 回复取消订阅请求
        /// </summary>
        /// <param name="ctx">当前连接在链上的节点上下文</param>
        /// <param name="chatMessage">取消订阅请求，内容为频道名</param>
        /// <returns>应答写出完成后结束的任务</returns>
        private Task ReplyUnsubscribeAsync(IChannelHandlerContext ctx, ChatMessage chatMessage)
        {
            var result = _channelList.Unsubscribe(chatMessage.Content, ctx.ChannelId) ? "1" : "0";

            return ReplyAsync(ctx, new ChatMessage(ChatMessageType.UnSubscribeAnswer, result));
        }

        /// <summary>
        /// 转发频道消息：填入发送者与时间后发给同频道其他成员
        /// </summary>
        /// <param name="ctx">当前连接在链上的节点上下文</param>
        /// <param name="chatMessage">频道消息请求</param>
        /// <returns>全部转发写出完成后结束的任务</returns>
        private async Task ReplyChannelMessageAsync(IChannelHandlerContext ctx, ChatMessage chatMessage)
        {
            var channelMsg = SerializeHelper.Deserialize<ChannelMessage>(chatMessage.Content);

            if (channelMsg == null || string.IsNullOrEmpty(channelMsg.Name))
                return;

            channelMsg.Sender = ctx.ChannelId;

            channelMsg.Sended = DateTimeHelper.ToString();

            var channel = _channelList.Get(channelMsg.Name);

            if (channel == null || channel.Members == null)
                return;

            var forwarded = new ChatMessage(ChatMessageType.ChannelMessage, SerializeHelper.Serialize(channelMsg));

            var members = channel.Members.ToArray();

            var sending = new List<Task>(members.Length);

            for (var i = 0; i < members.Length; i++)
            {
                if (members[i].ID != ctx.ChannelId)
                    sending.Add(ForwardAsync(members[i].ID, forwarded));
            }

            await Task.WhenAll(sending).ConfigureAwait(false);
        }

        /// <summary>
        /// 转发私聊消息：先应答发送方，再把填入发送者与时间的消息转给接收者
        /// </summary>
        /// <param name="ctx">当前连接在链上的节点上下文</param>
        /// <param name="chatMessage">私聊消息请求</param>
        /// <returns>应答与转发写出完成后结束的任务</returns>
        private async Task ReplyPrivateMessageAsync(IChannelHandlerContext ctx, ChatMessage chatMessage)
        {
            await ReplyAsync(ctx, new ChatMessage(ChatMessageType.PrivateMessageAnswer, "")).ConfigureAwait(false);

            var privateMessage = SerializeHelper.Deserialize<PrivateMessage>(chatMessage.Content);

            if (privateMessage == null || string.IsNullOrEmpty(privateMessage.Receiver))
                return;

            privateMessage.Sender = ctx.ChannelId;

            privateMessage.Sended = DateTimeHelper.ToString();

            await ForwardAsync(
                privateMessage.Receiver,
                new ChatMessage(ChatMessageType.PrivateMessage, SerializeHelper.Serialize(privateMessage)))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 回复创建群组请求
        /// </summary>
        /// <param name="ctx">当前连接在链上的节点上下文</param>
        /// <param name="chatMessage">创建群组请求，内容为群名</param>
        /// <returns>应答写出完成后结束的任务</returns>
        private Task ReplyCreateGroupAsync(IChannelHandlerContext ctx, ChatMessage chatMessage)
        {
            var result = _groupList.Create(chatMessage.Content, ctx.ChannelId) ? "1" : "0";

            return ReplyAsync(ctx, new ChatMessage(ChatMessageType.CreateGroupAnswer, result));
        }

        /// <summary>
        /// 回复解散群组请求
        /// </summary>
        /// <param name="ctx">当前连接在链上的节点上下文</param>
        /// <param name="chatMessage">解散群组请求，内容为群名</param>
        /// <returns>应答写出完成后结束的任务</returns>
        private Task ReplyRemoveGroupAsync(IChannelHandlerContext ctx, ChatMessage chatMessage)
        {
            var result = _groupList.Remove(chatMessage.Content) ? "1" : "0";

            return ReplyAsync(ctx, new ChatMessage(ChatMessageType.RemoveGroupAnswer, result));
        }

        /// <summary>
        /// 回复加入群组请求
        /// </summary>
        /// <param name="ctx">当前连接在链上的节点上下文</param>
        /// <param name="chatMessage">加入群组请求，内容为群名</param>
        /// <returns>应答写出完成后结束的任务</returns>
        private Task ReplyAddMemberAsync(IChannelHandlerContext ctx, ChatMessage chatMessage)
        {
            var result = _groupList.Enter(chatMessage.Content, ctx.ChannelId) ? "1" : "0";

            return ReplyAsync(ctx, new ChatMessage(ChatMessageType.AddMemberAnswer, result));
        }

        /// <summary>
        /// 回复退出群组请求
        /// </summary>
        /// <param name="ctx">当前连接在链上的节点上下文</param>
        /// <param name="chatMessage">退出群组请求，内容为群名</param>
        /// <returns>应答写出完成后结束的任务</returns>
        private Task ReplyRemoveMemberAsync(IChannelHandlerContext ctx, ChatMessage chatMessage)
        {
            var result = _groupList.Leave(chatMessage.Content, ctx.ChannelId) ? "1" : "0";

            return ReplyAsync(ctx, new ChatMessage(ChatMessageType.RemoveMemberAnswer, result));
        }

        /// <summary>
        /// 转发群组消息：填入发送者与时间后发给群内其他成员
        /// </summary>
        /// <param name="ctx">当前连接在链上的节点上下文</param>
        /// <param name="chatMessage">群组消息请求</param>
        /// <returns>应答与全部转发写出完成后结束的任务</returns>
        private async Task ReplyGroupMessageAsync(IChannelHandlerContext ctx, ChatMessage chatMessage)
        {
            await ReplyAsync(ctx, new ChatMessage(ChatMessageType.GroupMessageAnswer, "")).ConfigureAwait(false);

            var groupMsg = chatMessage.GetIMessage<GroupMessage>();

            if (groupMsg == null || string.IsNullOrEmpty(groupMsg.Name))
                return;

            groupMsg.Sender = ctx.ChannelId;

            groupMsg.Sended = DateTimeHelper.ToString();

            var group = _groupList.Get(groupMsg.Name);

            if (group == null || group.Members == null)
                return;

            // 成员快照在锁内取走，转发本身不持锁，避免慢连接阻塞群组维护。
            GroupMemberInfo[] members;

            lock (_groupList.SyncLocker)
            {
                members = group.Members.ToArray();
            }

            var forwarded = new ChatMessage(ChatMessageType.GroupMessage, SerializeHelper.Serialize(groupMsg));

            var sending = new List<Task>(members.Length);

            for (var i = 0; i < members.Length; i++)
            {
                if (members[i].ID != ctx.ChannelId)
                    sending.Add(ForwardAsync(members[i].ID, forwarded));
            }

            await Task.WhenAll(sending).ConfigureAwait(false);
        }

        #endregion

        /// <summary>
        /// 入站分发处理器：把解码后的消息交给内置处理，再原样传给下游使用者处理器。
        /// </summary>
        private sealed class InboundDispatcher : InboundHandlerAdapter
        {
            private readonly MessageServer _owner;

            /// <summary>
            /// 绑定所属服务端
            /// </summary>
            /// <param name="owner">所属服务端实例</param>
            public InboundDispatcher(MessageServer owner)
            {
                _owner = owner;
            }

            /// <summary>
            /// 处理一条已解码的消息；非消息对象原样下传
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <param name="msg">已解码的消息</param>
            /// <returns>内置处理与下游处理完成后结束的任务</returns>
            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var chatMessage = msg as ChatMessage;

                if (chatMessage == null)
                {
                    await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
                    return;
                }

                try
                {
                    await _owner.DispatchAsync(ctx, chatMessage).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // 单条业务消息处理失败不影响同连接的后续消息，与既有行为一致，只记录不外抛。
                    LogHelper.Error("MessageServer 消息处理失败:" + ctx.ChannelId, ex);
                }

                // 内置处理完成后仍下传，使用者处理器可据此做审计与统计。
                await ctx.FireChannelReadAsync(chatMessage).ConfigureAwait(false);
            }
        }
    }
}