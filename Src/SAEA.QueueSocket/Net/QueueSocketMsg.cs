/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.QueueSocket.Net
*文件名： QueueSocketMsg
*版本号： v7.0.0.1
*唯一标识：69a2f1bc-89b4-4e9b-be5b-ae24301d0409
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/5 18:06:01
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/5 18:06:01
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using System;

using SAEA.QueueSocket.Type;

namespace SAEA.QueueSocket.Net
{
    /// <summary>
    /// 队列消息实体
    /// </summary>
    public class QueueSocketMsg : IDisposable
    {
        // 定义一个常量MINLENGTH，表示消息的最小长度
        public const int MINLENGTH = (1 + 4 + 4 + 0 + 4 + 0 + 0);

        // 定义一个属性Total，用于获取或设置消息的总长度
        public int Total
        {
            get; set;
        }

        // 定义一个属性Type，用于获取或设置消息的类型
        public QueueSocketMsgType Type
        {
            get; set;
        }

        // 定义一个属性NameLength，用于获取或设置消息名称的长度
        public int NameLength
        {
            get; set;
        }

        // 定义一个属性Name，用于获取或设置消息的名称
        public string Name
        {
            get; set;
        }

        // 定义一个属性TopicLength，用于获取或设置消息主题的长度
        public int TopicLength
        {
            get; set;
        }

        // 定义一个属性Topic，用于获取或设置消息的主题
        public string Topic
        {
            get; set;
        }

        // 定义一个属性Data，用于获取或设置消息的数据
        public byte[] Data
        {
            get; set;
        }

        // 构造函数，用于创建一个指定类型的消息
        public QueueSocketMsg(QueueSocketMsgType type) : this(type, null)
        {

        }

        // 构造函数，用于创建一个指定类型和名称的消息
        public QueueSocketMsg(QueueSocketMsgType type, string name) : this(type, name, null)
        {

        }

        // 构造函数，用于创建一个指定类型、名称和主题的消息
        public QueueSocketMsg(QueueSocketMsgType type, string name, string topic) : this(type, name, topic, null)
        {

        }

        // 构造函数，用于创建一个指定类型、名称、主题和数据的消息
        public QueueSocketMsg(QueueSocketMsgType type, string name, string topic, byte[] data)
        {
            this.Type = type; // 设置消息类型
            this.Name = name; // 设置消息名称
            this.Topic = topic; // 设置消息主题
            this.Data = data; // 设置消息数据
        }

        // 实现IDisposable接口的Dispose方法，用于释放资源
        public void Dispose()
        {
            // 如果消息数据不为空且长度大于0，则清空数据
            if (this.Data != null && Data.Length > 0)
            {
                Array.Clear(Data, 0, Data.Length);
            }
            // 将总长度、名称长度和主题长度设置为0
            this.Total = this.NameLength = this.TopicLength;
            // 将消息类型设置为0
            this.Type = 0;
        }
    }
}
