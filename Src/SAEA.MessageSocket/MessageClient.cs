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
*版本号： v26.4.23.1
*唯一标识：e6bb7b6a-f8e7-4813-9565-71ee99a34447
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：MessageClient接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:20:17
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Common.Serialization;
using SAEA.Common.Threading;
using SAEA.MessageSocket.Model;
using SAEA.MessageSocket.Model.Business;
using SAEA.MessageSocket.Model.Communication;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Model;

using System;
using System.Buffers;
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

        /// <summary>
        /// 连接建立后触发，参数为当前客户端实例；异步连接失败时不触发
        /// </summary>
        public event Action<MessageClient> OnConnected;

        /// <summary>
        /// 收到私聊消息时触发，在独立任务中执行
        /// </summary>
        public event Action<PrivateMessage> OnPrivateMessage;

        /// <summary>
        /// 收到频道消息时触发，在独立任务中执行
        /// </summary>
        public event Action<ChannelMessage> OnChannelMessage;

        /// <summary>
        /// 收到群组消息时触发，在独立任务中执行
        /// </summary>
        public event Action<GroupMessage> OnGroupMessage;

        /// <summary>
        /// 接收内容解析失败时触发，参数为本端用户标识与异常
        /// </summary>
        public event OnErrorHandler OnError;

        IClientSocket _client;

        MessageContext _messageContext;

        Batcher _batcher;

        /// <summary>
        /// 客户端标识，取服务端分配的用户标识；尚未分配时退化为本地端点地址并写回上下文
        /// </summary>
        public string ID
        {
            get
            {
                if (string.IsNullOrEmpty(_messageContext.UserToken.ID))
                {
                    _messageContext.UserToken.ID = _client.Endpoint;
                }

                return _messageContext.UserToken.ID;
            }
        }

        /// <summary>
        /// 创建客户端，注册接收回调，初始化批量发送队列并启动心跳任务
        /// </summary>
        /// <param name="bufferSize">收发缓冲区大小（字节）</param>
        /// <param name="ip">服务器地址</param>
        /// <param name="port">服务器端口</param>
        public MessageClient(int bufferSize = 102400, string ip = "127.0.0.1", int port = 39654)
        {
            _messageContext = new MessageContext();

            var option = SocketOptionBuilder.Instance
                .SetSocket()
                .UsePacket(_messageContext)
                .SetIP(ip)
                .SetPort(port)
                .SetReadBufferSize(bufferSize)
                .SetWriteBufferSize(bufferSize)
                .Build();

            _client = SocketFactory.CreateClientSocket(option);

            _client.OnClientReceiveSpan += _client_OnReceiveSpan;

            HeartSpan = 30 * 1000;

            HeartAsync();

            _batcher = new Batcher(10000, 10, 500000);

            _batcher.OnBatched += _batcher_OnBatched;
        }

        private void _batcher_OnBatched(IBatcher sender, byte[] data)
        {
            _client.Send(data.AsSpan());
            _messageContext.UserToken.Actived = DateTimeHelper.Now;
        }

        private void _client_OnReceiveSpan(ReadOnlySpan<byte> dataSpan)
        {
            var data = dataSpan.ToArray();

            _messageContext.UserToken.Actived = DateTimeHelper.Now;
            using (var msgs = _messageContext.Unpacker.Decode(new ReadOnlySequence<byte>(data)))
            {
                if (msgs.Count < 1) return;

                foreach (var msg in msgs.Frames)
                {
                    if (msg.Content.Length == 0)
                    {
                        continue;
                    }
                    try
                    {
                        var cm = SerializeHelper.PBDeserialize<ChatMessage>(msg.Content.ToArray());

                        switch (cm.Type)
                        {
                            case ChatMessageType.LoginAnswer:
                                this.Logined = true;
                                break;
                            case ChatMessageType.SubscribeAnswer:
                                if (cm.Content == "1")
                                {
                                    _subscribed = true;
                                }
                                else
                                {
                                    _subscribed = false;
                                }
                                break;
                            case ChatMessageType.UnSubscribeAnswer:
                                if (cm.Content == "1")
                                {
                                    _unsubscribed = true;
                                }
                                else
                                {
                                    _unsubscribed = false;
                                }
                                break;
                            case ChatMessageType.ChannelMessage:
                                TaskHelper.Run(() => OnChannelMessage?.Invoke(cm.GetIMessage<ChannelMessage>()));
                                break;
                            case ChatMessageType.PrivateMessage:
                                TaskHelper.Run(() => OnPrivateMessage?.Invoke(cm.GetIMessage<PrivateMessage>()));
                                break;
                            case ChatMessageType.GroupMessage:
                                TaskHelper.Run(() => OnGroupMessage?.Invoke(cm.GetIMessage<GroupMessage>()));
                                break;
                            case ChatMessageType.PrivateMessageAnswer:
                                break;
                            case ChatMessageType.CreateGroupAnswer:
                            case ChatMessageType.RemoveGroupAnswer:
                            case ChatMessageType.AddMemberAnswer:
                            case ChatMessageType.RemoveMemberAnswer:
                                break;

                            case ChatMessageType.GroupMessageAnswer:
                                break;
                            default:
                                ConsoleHelper.WriteLine("cm.Type", cm.Type);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke(_messageContext.UserToken.ID, ex);
                    }
                }
            }
        }


        void HeartAsync()
        {
            TaskHelper.Run(() =>
            {
                try
                {
                    while (true)
                    {
                        if (_client.Connected)
                        {
                            ThreadHelper.Sleep(1000);
                            
                            if (_messageContext.UserToken.Actived.AddMilliseconds(HeartSpan) <= DateTimeHelper.Now)
                            {
                                var sm = new BaseSocketProtocal((byte)SocketProtocalType.Heart, ReadOnlyMemory<byte>.Empty);
                                using (var w = new PooledBufferWriter(64))
                                {
                                    sm.WriteTo(w);
                                    _client.Send(w.WrittenSpan);
                                }
                            }
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
        /// 异步发起连接，成功后触发 OnConnected；连接失败时在回调线程抛出 KernelException
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
                    OnConnected?.Invoke(this);
                });
            }
        }

        /// <summary>
        /// 同步连接服务器，连接后触发 OnConnected
        /// </summary>
        public void Connect()
        {
            if (!_client.Connected)
            {
                _client.Connect();
                OnConnected?.Invoke(this);
            }
        }

        private void SendBase(ChatMessage cm)
        {
            var data = SerializeHelper.PBSerialize(cm);

            byte[] content;
            using (var w = new PooledBufferWriter(64))
            {
                BaseSocketProtocal.Parse(data, SocketProtocalType.ChatMessage).WriteTo(w);
                content = w.WrittenSpan.ToArray();
            }

            _batcher.Insert(content);
        }

        private async Task SendBaseWithBackpressureAsync(ChatMessage cm, int timeoutMs = 5000)
        {
            var data = SerializeHelper.PBSerialize(cm);

            byte[] content;
            using (var w = new PooledBufferWriter(64))
            {
                BaseSocketProtocal.Parse(data, SocketProtocalType.ChatMessage).WriteTo(w);
                content = w.WrittenSpan.ToArray();
            }

            await _batcher.InsertWithBackpressureAsync(content, timeoutMs);
        }


        /// <summary>
        /// 发送登录请求，收到服务端应答后 Logined 才置为 true
        /// </summary>
        public void Login()
        {
            SendBase(new ChatMessage(ChatMessageType.Login, ""));
        }

        /// <summary>
        /// 发送频道订阅请求并返回本地记录的订阅状态，应答异步到达，返回值可能仍是上次的结果
        /// </summary>
        /// <param name="name">频道名称</param>
        /// <returns>本地记录的订阅状态，不代表本次请求的处理结果</returns>
        public bool Subscribe(string name)
        {
            SendBase(new ChatMessage(ChatMessageType.Subscribe, name));
            return _subscribed;
        }

        /// <summary>
        /// 发送取消订阅请求并返回本地记录的退订状态，应答异步到达，返回值可能仍是上次的结果
        /// </summary>
        /// <param name="name">频道名称</param>
        /// <returns>本地记录的退订状态，不代表本次请求的处理结果</returns>
        public bool Unsubscribe(string name)
        {
            SendBase(new ChatMessage(ChatMessageType.UnSubscribe, name));
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

            SendBase(new ChatMessage(ChatMessageType.ChannelMessage, cm));
        }


        /// <summary>
        /// 发送私聊消息，不等待批量队列容量，队列已满时本条消息被丢弃
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

            SendBase(new ChatMessage(ChatMessageType.PrivateMessage, pm));
        }

        /// <summary>
        /// 异步发送私聊消息，在超时时间内等待批量队列容量，超时后静默丢弃本条消息
        /// </summary>
        /// <param name="receiver">接收者标识</param>
        /// <param name="content">消息正文</param>
        /// <param name="timeoutMs">等待队列容量的超时时间（毫秒）</param>
        /// <returns>发送任务，不反映消息是否真正入队</returns>
        public async Task SendPrivateMsgAsync(string receiver, string content, int timeoutMs = 5000)
        {
            PrivateMessage pm = new PrivateMessage()
            {
                Receiver = receiver,
                Content = content
            };

            await SendBaseWithBackpressureAsync(new ChatMessage(ChatMessageType.PrivateMessage, pm), timeoutMs);
        }

        #region group

        /// <summary>
        /// 发送创建群组请求，创建成功后发起者自动成为群成员，群名重复时服务端应答失败
        /// </summary>
        /// <param name="groupName">群组名称</param>
        public void SendCreateGroup(string groupName)
        {
            SendBase(new ChatMessage(ChatMessageType.CreateGroup, groupName));
        }

        /// <summary>
        /// 发送解散群组请求，服务端仅按群名删除且不校验发起者是否为群主
        /// </summary>
        /// <param name="groupName">群组名称</param>
        public void SendRemoveGroup(string groupName)
        {
            SendBase(new ChatMessage(ChatMessageType.RemoveGroup, groupName));
        }

        /// <summary>
        /// 发送加入群组请求，把当前用户加入指定群组，已在群内时服务端应答失败
        /// </summary>
        /// <param name="groupName">群组名称</param>
        public void SendAddMember(string groupName)
        {
            SendBase(new ChatMessage(ChatMessageType.AddMember, groupName));
        }

        /// <summary>
        /// 发送退出群组请求，普通成员只移除自己，群主退出时服务端直接解散该群组
        /// </summary>
        /// <param name="groupName">群组名称</param>
        public void SendRemoveMember(string groupName)
        {
            SendBase(new ChatMessage(ChatMessageType.RemoveMember, groupName));
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

            SendBase(new ChatMessage(ChatMessageType.GroupMessage, pm));
        }



        #endregion



    }
}