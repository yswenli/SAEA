/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.MessageSocket
*文件名： MessageClient
*版本号： v5.0.0.1
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
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.MessageSocket.Model;
using SAEA.MessageSocket.Model.Business;
using SAEA.MessageSocket.Model.Communication;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;
using System.Threading;

namespace SAEA.MessageSocket
{
    public class MessageClient
    {
        int _bufferSize = 100 * 1024;

        byte[] _buffer;

        public bool Logined
        {
            get; set;
        } = false;

        bool _subscribed = false;

        bool _unsubscribed = false;


        AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
        private DateTime Actived = DateTimeHelper.Now;
        private int HeartSpan;


        public event Action<PrivateMessage> OnPrivateMessage;

        public event Action<ChannelMessage> OnChannelMessage;

        public event Action<GroupMessage> OnGroupMessage;

        public event OnErrorHandler OnError;

        IClientSocket _client;

        MessageContext _messageContext;

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

        public MessageClient(int bufferSize = 1024, string ip = "127.0.0.1", int port = 39654)
        {
            _bufferSize = bufferSize;
            _buffer = new byte[_bufferSize];

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
        }

        private void _client_OnReceive(byte[] data)
        {
            Actived = DateTimeHelper.Now;

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
                                    _autoResetEvent.Set();
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
                                    _autoResetEvent.Set();
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
                                    _autoResetEvent.Set();
                                    break;
                                case ChatMessageType.ChannelMessage:
                                    TaskHelper.Start(() => OnChannelMessage?.Invoke(cm.GetIMessage<ChannelMessage>()));
                                    break;
                                case ChatMessageType.PrivateMessage:
                                    TaskHelper.Start(() => OnPrivateMessage?.Invoke(cm.GetIMessage<PrivateMessage>()));
                                    break;
                                case ChatMessageType.GroupMessage:
                                    TaskHelper.Start(() => OnGroupMessage?.Invoke(cm.GetIMessage<GroupMessage>()));
                                    break;
                                case ChatMessageType.PrivateMessageAnswer:
                                    _autoResetEvent.Set();
                                    break;
                                case ChatMessageType.CreateGroupAnswer:
                                case ChatMessageType.RemoveGroupAnswer:
                                case ChatMessageType.AddMemberAnswer:
                                case ChatMessageType.RemoveMemberAnswer:
                                    _autoResetEvent.Set();
                                    break;

                                case ChatMessageType.GroupMessageAnswer:
                                    _autoResetEvent.Set();
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
            TaskHelper.Start(() =>
            {
                try
                {
                    while (true)
                    {
                        if (_client.Connected)
                        {
                            if (Actived.AddMilliseconds(HeartSpan) <= DateTimeHelper.Now)
                            {
                                var sm = new BaseSocketProtocal()
                                {
                                    BodyLength = 0,
                                    Type = (byte)SocketProtocalType.Heart
                                };
                                _client.SendAsync(sm.ToBytes());
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

        public void Connect()
        {
            _client.ConnectAsync().GetAwaiter().GetResult();
        }

        private void SendBase(ChatMessage cm)
        {
            var data = SerializeHelper.PBSerialize(cm);

            var content = BaseSocketProtocal.Parse(data, SocketProtocalType.ChatMessage).ToBytes();

            _client.SendAsync(content);

            Actived = DateTimeHelper.Now;
        }


        public void Login()
        {
            SendBase(new ChatMessage(ChatMessageType.Login, ""));
            _autoResetEvent.WaitOne();
        }

        public bool Subscribe(string name)
        {
            SendBase(new ChatMessage(ChatMessageType.Subscribe, name));
            _autoResetEvent.WaitOne();
            return _subscribed;
        }

        public bool Unsubscribe(string name)
        {
            SendBase(new ChatMessage(ChatMessageType.UnSubscribe, name));
            _autoResetEvent.WaitOne();
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

            _autoResetEvent.WaitOne();
        }

        #region group

        public void SendCreateGroup(string groupName)
        {
            SendBase(new ChatMessage(ChatMessageType.CreateGroup, groupName));
            _autoResetEvent.WaitOne();
        }

        public void SendRemoveGroup(string groupName)
        {
            SendBase(new ChatMessage(ChatMessageType.RemoveGroup, groupName));
            _autoResetEvent.WaitOne();
        }

        public void SendAddMember(string groupName)
        {
            SendBase(new ChatMessage(ChatMessageType.AddMember, groupName));
            _autoResetEvent.WaitOne();
        }

        public void SendRemoveMember(string groupName)
        {
            SendBase(new ChatMessage(ChatMessageType.RemoveMember, groupName));
            _autoResetEvent.WaitOne();
        }

        public void SendGroupMessage(string groupName, string content)
        {
            GroupMessage pm = new GroupMessage()
            {
                Name = groupName,
                Content = content
            };

            SendBase(new ChatMessage(ChatMessageType.GroupMessage, pm));
            _autoResetEvent.WaitOne();
        }



        #endregion



    }
}
