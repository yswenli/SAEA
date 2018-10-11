/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.MessageSocket
*文件名： Class1
*版本号： V2.1.5.2
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
*版本号： V2.1.5.2
*描述：
*
*****************************************************************************/

using SAEA.MessageSocket.Collection;
using SAEA.MessageSocket.Common;
using SAEA.MessageSocket.Model;
using SAEA.MessageSocket.Model.Business;
using SAEA.MessageSocket.Model.Communication;
using SAEA.Common;
using SAEA.Sockets;
using SAEA.Sockets.Core;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;

namespace SAEA.MessageSocket
{
    public class MessageServer : BaseServerSocket
    {

        ChannelList _channelList = new ChannelList();

        GroupList _groupList = new GroupList();


        public MessageServer(int bufferSize = 1024, int count = 1000000, int timeOut = 60 * 1000) : base(new MessageContext(), bufferSize, count, true, timeOut)
        {

        }

        protected override void OnReceiveBytes(IUserToken userToken, byte[] data)
        {
            var mUserToken = (MessageUserToken)userToken;

            userToken.Coder.Pack(data, null, (s) =>
            {
                if (s.Content != null)
                {
                    try
                    {
                        var cm = ConvertHelper.PBDeserialize<ChatMessage>(s.Content);

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
                        Disconnect(userToken, ex);
                    }
                }

            }, null);
        }


        #region reply
        void ReplyBase(IUserToken userToken, ChatMessage cm)
        {
            var data = ConvertHelper.PBSerialize(cm);

            var sp = SocketProtocal.Parse(data, SocketProtocalType.ChatMessage).ToBytes();

            SendAsync(userToken, sp);
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
            var channelMsg = ConvertHelper.Deserialize<ChannelMessage>(cm.Content);

            if (channelMsg != null && !string.IsNullOrEmpty(channelMsg.Name))
            {
                channelMsg.Sender = userToken.ID;

                channelMsg.Sended = DateTimeHelper.ToString();

                var channel = _channelList.Get(channelMsg.Name);

                if (channel != null && channel.Members != null)
                {
                    lock (_channelList.SyncLocker)
                    {
                        foreach (var m in channel.Members)
                        {
                            try
                            {
                                if (m.ID != userToken.ID)
                                {
                                    var r = SessionManager.Get(m.ID);
                                    if (r != null)
                                    {
                                        ReplyBase(r, new ChatMessage(ChatMessageType.ChannelMessage, ConvertHelper.Serialize(channelMsg)));
                                    }
                                }

                            }
                            catch { }
                        }
                    }
                }
            }
        }


        void ReplyPrivateMessage(MessageUserToken userToken, ChatMessage cm)
        {
            ReplyBase(userToken, new ChatMessage(ChatMessageType.PrivateMessageAnswer, ""));

            var privateMessage = ConvertHelper.Deserialize<PrivateMessage>(cm.Content);

            if (privateMessage != null && !string.IsNullOrEmpty(privateMessage.Receiver))
            {
                privateMessage.Sender = userToken.ID;

                privateMessage.Sended = DateTimeHelper.ToString();

                var r = SessionManager.Get(privateMessage.Receiver);
                if (r != null)
                    ReplyBase(r, new ChatMessage(ChatMessageType.PrivateMessage, ConvertHelper.Serialize(privateMessage)));
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
                        foreach (var m in group.Members)
                        {
                            try
                            {
                                if (m.ID != userToken.ID)
                                {
                                    var r = SessionManager.Get(m.ID);
                                    if (r != null)
                                        ReplyBase(r, new ChatMessage(ChatMessageType.GroupMessage, groupMsg));
                                }

                            }
                            catch { }
                        }
                    }
                }
            }
        }

        #endregion



    }
}
