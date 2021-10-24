/****************************************************************************
*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.MessageSocket
*文件名： MessageClient
*版本号： v7.0.0.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
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

namespace SAEA.MessageSocket
{
    public class MessageClient
    {
        public bool Logined
        {
            get; set;
        } = false;

        bool _subscribed = false;

        bool _unsubscribed = false;

        private int HeartSpan;

        public event Action<MessageClient> OnConnected;

        public event Action<PrivateMessage> OnPrivateMessage;

        public event Action<ChannelMessage> OnChannelMessage;

        public event Action<GroupMessage> OnGroupMessage;

        public event OnErrorHandler OnError;

        IClientSocket _client;

        MessageContext _messageContext;

        Batcher _batcher;

        public string ID
        {
            get
            {
                if (string.IsNullOrEmpty(_messageContext.UserToken.ID))
                {
                    _messageContext.UserToken.ID = _messageContext.UserToken.Socket.RemoteEndPoint.ToString();
                }

                return _messageContext.UserToken.ID;
            }
        }

        public MessageClient(int bufferSize = 102400, string ip = "127.0.0.1", int port = 39654)
        {
            _messageContext = new MessageContext();

            var option = SocketOptionBuilder.Instance
                .SetSocket()
                .UseIocp(_messageContext)
                .SetIP(ip)
                .SetPort(port)
                .SetReadBufferSize(bufferSize)
                .SetWriteBufferSize(bufferSize)
                .Build();

            _client = SocketFactory.CreateClientSocket(option);

            _client.OnReceive += _client_OnReceive;

            HeartSpan = 10 * 1000;

            HeartAsync();

            _batcher = new Batcher(10000, 10);

            _batcher.OnBatched += _batcher_OnBatched;
        }

        private void _batcher_OnBatched(IBatcher sender, byte[] data)
        {
            _client.Send(data);
        }

        private void _client_OnReceive(byte[] data)
        {
            if (data != null)
            {
                this._messageContext.Unpacker.Unpack(data, (s) =>
                {
                    if (s.Content != null)
                    {
                        try
                        {
                            var cm = SerializeHelper.PBDeserialize<ChatMessage>(s.Content);

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

                }, null, null);
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
                            if (_messageContext.UserToken.Actived.AddMilliseconds(HeartSpan) <= DateTimeHelper.Now)
                            {
                                var sm = new BaseSocketProtocal()
                                {
                                    BodyLength = 0,
                                    Type = (byte)SocketProtocalType.Heart
                                };
                                _client.Send(sm.ToBytes());
                            }
                            ThreadHelper.Sleep(HeartSpan);
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

            var content = BaseSocketProtocal.Parse(data, SocketProtocalType.ChatMessage).ToBytes();

            _batcher.Insert(content);
        }


        public void Login()
        {
            SendBase(new ChatMessage(ChatMessageType.Login, ""));
        }

        public bool Subscribe(string name)
        {
            SendBase(new ChatMessage(ChatMessageType.Subscribe, name));
            return _subscribed;
        }

        public bool Unsubscribe(string name)
        {
            SendBase(new ChatMessage(ChatMessageType.UnSubscribe, name));
            return _unsubscribed;
        }

        public void SendChannelMsg(string channelName, string content)
        {
            ChannelMessage cm = new ChannelMessage()
            {
                Name = channelName,
                Content = content
            };

            SendBase(new ChatMessage(ChatMessageType.ChannelMessage, cm));
        }


        public void SendPrivateMsg(string receiver, string content)
        {
            PrivateMessage pm = new PrivateMessage()
            {
                Receiver = receiver,
                Content = content
            };

            SendBase(new ChatMessage(ChatMessageType.PrivateMessage, pm));
        }

        #region group

        public void SendCreateGroup(string groupName)
        {
            SendBase(new ChatMessage(ChatMessageType.CreateGroup, groupName));
        }

        public void SendRemoveGroup(string groupName)
        {
            SendBase(new ChatMessage(ChatMessageType.RemoveGroup, groupName));
        }

        public void SendAddMember(string groupName)
        {
            SendBase(new ChatMessage(ChatMessageType.AddMember, groupName));
        }

        public void SendRemoveMember(string groupName)
        {
            SendBase(new ChatMessage(ChatMessageType.RemoveMember, groupName));
        }

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
