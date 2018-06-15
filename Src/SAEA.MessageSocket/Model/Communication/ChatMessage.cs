/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.MessageSocket
*文件名： Class1
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/

using SAEA.MessageSocket.Common;
using ProtoBuf;
using SAEA.Common;
using System;

namespace SAEA.MessageSocket.Model.Communication
{
    [Serializable, ProtoContract]
    class ChatMessage
    {
        [ProtoMember(1)]
        public ChatMessageType Type
        {
            get; set;
        }
        [ProtoMember(2)]
        public string Content
        {
            get; set;
        }
        [ProtoMember(3)]
        public int DateTime
        {
            get; set;
        }

        public ChatMessage()
        {

        }

        public ChatMessage(ChatMessageType type, string content)
        {
            this.Type = type;
            this.Content = content;
            this.DateTime = DateTimeHelper.GetUnixTick();
        }

        public ChatMessage(ChatMessageType type, IMessage msg)
        {
            this.Type = type;
            this.Content = ConvertHelper.Serialize(msg);
            this.DateTime = DateTimeHelper.GetUnixTick();
        }

        public T GetIMessage<T>() where T : IMessage
        {
            if (!string.IsNullOrEmpty(this.Content))
            {
                return ConvertHelper.Deserialize<T>(this.Content);
            }
            return default(T);
        }
    }
}
