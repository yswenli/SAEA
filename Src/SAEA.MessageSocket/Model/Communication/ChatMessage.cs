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
*命名空间：SAEA.MessageSocket.Model.Communication
*文件名： ChatMessage
*版本号： v26.4.23.1
*唯一标识：35f75927-7220-40f2-9f01-3789acf70eae
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：ChatMessage消息类
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 11:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：改为公开类型，供处理器链下游消费解码后的消息，并补全 XML 注释
*
*****************************************************************************/
using ProtoBuf;
using SAEA.Common;
using SAEA.Common.Serialization;
using System;

namespace SAEA.MessageSocket.Model.Communication
{
    /// <summary>
    /// 消息信封：协议类型 + 内容 + 发送时间。内容既可是纯文本（订阅名、群名、应答结果），
    /// 也可是领域对象的序列化文本，由 <see cref="GetIMessage{T}"/> 还原。
    /// </summary>
    [Serializable, ProtoContract]
    public class ChatMessage
    {
        /// <summary>
        /// 协议类型，决定服务端与客户端如何处理本条消息
        /// </summary>
        [ProtoMember(1)]
        public ChatMessageType Type
        {
            get; set;
        }

        /// <summary>
        /// 消息内容：纯文本或领域对象的序列化文本
        /// </summary>
        [ProtoMember(2)]
        public string Content
        {
            get; set;
        }

        /// <summary>
        /// 发送时间的 Unix 秒级时间戳
        /// </summary>
        [ProtoMember(3)]
        public int DateTime
        {
            get; set;
        }

        /// <summary>
        /// 创建空信封，供反序列化使用
        /// </summary>
        public ChatMessage()
        {

        }

        /// <summary>
        /// 以纯文本内容创建信封
        /// </summary>
        /// <param name="type">协议类型</param>
        /// <param name="content">消息内容</param>
        public ChatMessage(ChatMessageType type, string content)
        {
            this.Type = type;
            this.Content = content;
            this.DateTime = DateTimeHelper.GetUnixTick();
        }

        /// <summary>
        /// 以领域对象创建信封，内容保存该对象的序列化文本
        /// </summary>
        /// <param name="type">协议类型</param>
        /// <param name="msg">领域对象</param>
        public ChatMessage(ChatMessageType type, IMessage msg)
        {
            this.Type = type;
            this.Content = SerializeHelper.Serialize(msg);
            this.DateTime = DateTimeHelper.GetUnixTick();
        }

        /// <summary>
        /// 把内容还原成领域对象
        /// </summary>
        /// <typeparam name="T">领域对象类型</typeparam>
        /// <returns>还原后的领域对象；内容为空时为 null</returns>
        public T GetIMessage<T>() where T : IMessage
        {
            if (!string.IsNullOrEmpty(this.Content))
            {
                return SerializeHelper.Deserialize<T>(this.Content);
            }
            return default(T);
        }
    }
}