/****************************************************************************
*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.MessageSocket
*文件名： Class1
*版本号： v6.0.0.1
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
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Common.Serialization;
using SAEA.MessageSocket.Collection;
using SAEA.MessageSocket.Model;
using SAEA.MessageSocket.Model.Business;
using SAEA.MessageSocket.Model.Communication;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;
using System.Threading.Tasks;

namespace SAEA.MessageSocket
{
    public class MessageServer
    {

        IServerSocket _server;

        ChannelList _channelList = new ChannelList();

        GroupList _groupList = new GroupList();


        ClassificationBatcher _classificationBatcher;


        public event OnAcceptedHandler OnAccepted;

        public event OnDisconnectedHandler OnDisconnected;

        public event OnErrorHandler OnError;


        public int ClientCounts
        {
            get
            {
                return _server.ClientCounts;
            }
        }



        public MessageServer(int port = 39654, int bufferSize = 102400, int count = 1000, int timeOut = 60 * 1000)
        {
            var option = SocketOptionBuilder.Instance
                .SetPort(port)
                .UseIocp(new MessageContext())
                .SetReadBufferSize(bufferSize)
                .SetWriteBufferSize(bufferSize)
                .SetCount(count)
                .SetTimeOut(timeOut)
                .Build();

            _server = SocketFactory.CreateServerSocket(option);

            _server.OnAccepted += _server_OnAccepted;

            _server.OnReceive += _server_OnReceive;

            _server.OnError += _server_OnError;

            _server.OnDisconnected += _server_OnDisconnected;

            _classificationBatcher = ClassificationBatcher.GetInstance(10000, 10);

            _classificationBatcher.OnBatched += _classificationBatcher_OnBatched;
        }

        private void _classificationBatcher_OnBatched(string id, byte[] data)
        {
            _server.SendAsync(id, data);
        }

        private void _server_OnError(string ID, Exception ex)
        {
            OnError?.Invoke(ID, ex);
        }

        private void _server_OnAccepted(object obj)
        {
            OnAccepted?.Invoke(obj);
        }

        private void _server_OnDisconnected(string ID, Exception ex)
        {
            this.OnDisconnected?.Invoke(ID, ex);
        }

        public void Start()
        {
            _server.Start();
        }

        private void _server_OnReceive(object currentObj, byte[] data)
        {
            var mUserToken = (MessageUserToken)currentObj;

            mUserToken.Unpacker.Unpack(data, (s) =>
            {
                if (s.Content != null)
                {
                    try
                    {
                        var cm = SerializeHelper.PBDeserialize<ChatMessage>(s.Content);

                        switch (cm.Type)
                        {
                            case ChatMessageType.Login:
                                ReplyLogin(mUserToken, cm);
                                break;
                            case ChatMessageType.Subscribe:
                                ReplySubscribe(mUserToken, cm);
                                break;
                            case ChatMessageType.UnSubscribe:
                                ReplyUnsubscribe(mUserToken, cm);
                                break;
                            case ChatMessageType.ChannelMessage:
                                ReplyChannelMessage(mUserToken, cm);
                                break;
                            case ChatMessageType.PrivateMessage:
                                ReplyPrivateMessage(mUserToken, cm);
                                break;
                            case ChatMessageType.CreateGroup:
                                ReplyCreateGroup(mUserToken, cm);
                                break;
                            case ChatMessageType.AddMember:
                                ReplyAddMember(mUserToken, cm);
                                break;
                            case ChatMessageType.RemoveMember:
                                ReplyRemoveMember(mUserToken, cm);
                                break;
                            case ChatMessageType.RemoveGroup:
                                ReplyRemoveGroup(mUserToken, cm);
                                break;
                            case ChatMessageType.GroupMessage:
                                ReplyGroupMessage(mUserToken, cm);
                                break;
                            default:
                                throw new Exception("未知的协议");
                        }

                    }
                    catch (Exception ex)
                    {
                        _server.Disconnect(mUserToken.ID);
                        LogHelper.Error("MessageServer._server_OnReceive", ex);
                    }
                }

            }, null, null);
        }

        public void Stop()
        {
            _server.Stop();
        }


        #region reply
        void ReplyBase(IUserToken userToken, ChatMessage cm)
        {
            var data = SerializeHelper.PBSerialize(cm);

            var sp = BaseSocketProtocal.Parse(data, SocketProtocalType.ChatMessage).ToBytes();

            _classificationBatcher.Insert(userToken.ID, sp);
        }

        void ReplyLogin(MessageUserToken userToken, ChatMessage cm)
        {
            userToken.Logined = DateTimeHelper.Now;

            ReplyBase(userToken, new ChatMessage(ChatMessageType.LoginAnswer, ""));
        }

        void ReplySubscribe(MessageUserToken userToken, ChatMessage cm)
        {
            string result = "0";

            if (_channelList.Subscribe(cm.Content, userToken.ID))
            {
                result = "1";
            }

            ReplyBase(userToken, new ChatMessage(ChatMessageType.SubscribeAnswer, result));
        }

        void ReplyUnsubscribe(MessageUserToken userToken, ChatMessage cm)
        {
            string result = "0";

            if (_channelList.Unsubscribe(cm.Content, userToken.ID))
            {
                result = "1";
            }

            ReplyBase(userToken, new ChatMessage(ChatMessageType.UnSubscribeAnswer, result));
        }


        void ReplyChannelMessage(MessageUserToken userToken, ChatMessage cm)
        {
            var channelMsg = SerializeHelper.Deserialize<ChannelMessage>(cm.Content);

            if (channelMsg != null && !string.IsNullOrEmpty(channelMsg.Name))
            {
                channelMsg.Sender = userToken.ID;

                channelMsg.Sended = DateTimeHelper.ToString();

                var channel = _channelList.Get(channelMsg.Name);

                if (channel != null && channel.Members != null)
                {
                    var members = channel.Members.ToArray();

                    var ccm = new ChatMessage(ChatMessageType.ChannelMessage, SerializeHelper.Serialize(channelMsg));

                    var data = SerializeHelper.PBSerialize(ccm);

                    var sp = BaseSocketProtocal.Parse(data, SocketProtocalType.ChatMessage).ToBytes();

                    Parallel.ForEach(members, (m) =>
                    {
                        if (m.ID != userToken.ID)
                        {
                            _server.SendAsync(m.ID, sp);
                        }
                    });
                }
            }
        }


        void ReplyPrivateMessage(MessageUserToken userToken, ChatMessage cm)
        {
            ReplyBase(userToken, new ChatMessage(ChatMessageType.PrivateMessageAnswer, ""));

            var privateMessage = SerializeHelper.Deserialize<PrivateMessage>(cm.Content);

            if (privateMessage != null && !string.IsNullOrEmpty(privateMessage.Receiver))
            {
                privateMessage.Sender = userToken.ID;

                privateMessage.Sended = DateTimeHelper.ToString();

                var r = (IUserToken)_server.GetCurrentObj(privateMessage.Receiver);
                if (r != null)
                    ReplyBase(r, new ChatMessage(ChatMessageType.PrivateMessage, SerializeHelper.Serialize(privateMessage)));
            }
        }


        void ReplyCreateGroup(MessageUserToken userToken, ChatMessage cm)
        {
            string result = "0";

            if (_groupList.Create(cm.Content, userToken.ID))
            {
                result = "1";
            }

            ReplyBase(userToken, new ChatMessage(ChatMessageType.CreateGroupAnswer, result));
        }

        void ReplyRemoveGroup(MessageUserToken userToken, ChatMessage cm)
        {
            string result = "0";

            if (_groupList.Remove(cm.Content))
            {
                result = "1";
            }

            ReplyBase(userToken, new ChatMessage(ChatMessageType.RemoveGroupAnswer, result));
        }

        void ReplyAddMember(MessageUserToken userToken, ChatMessage cm)
        {
            string result = "0";

            if (_groupList.Enter(cm.Content, userToken.ID))
            {
                result = "1";
            }

            ReplyBase(userToken, new ChatMessage(ChatMessageType.AddMemberAnswer, result));
        }

        void ReplyRemoveMember(MessageUserToken userToken, ChatMessage cm)
        {
            string result = "0";

            if (_groupList.Leave(cm.Content, userToken.ID))
            {
                result = "1";
            }

            ReplyBase(userToken, new ChatMessage(ChatMessageType.RemoveMemberAnswer, result));

        }


        void ReplyGroupMessage(MessageUserToken userToken, ChatMessage cm)
        {
            ReplyBase(userToken, new ChatMessage(ChatMessageType.GroupMessageAnswer, ""));

            var groupMsg = cm.GetIMessage<GroupMessage>();

            if (groupMsg != null && !string.IsNullOrEmpty(groupMsg.Name))
            {
                groupMsg.Sender = userToken.ID;

                groupMsg.Sended = DateTimeHelper.ToString();

                var group = _groupList.Get(groupMsg.Name);

                if (group != null && group.Members != null)
                {
                    lock (_groupList.SyncLocker)
                    {
                        var ccm = new ChatMessage(ChatMessageType.GroupMessage, groupMsg);
                        var data = SerializeHelper.PBSerialize(cm);
                        var sp = BaseSocketProtocal.Parse(data, SocketProtocalType.ChatMessage).ToBytes();

                        Parallel.ForEach(group.Members, (m) =>
                        {
                            if (m.ID != userToken.ID)
                            {
                                _server.SendAsync(m.ID, sp);
                            }
                        });
                    }
                }
            }
        }

        #endregion



    }
}
