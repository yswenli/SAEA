/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.QueueSocket
*文件名： QueueCoder
*版本号： V3.3.3.1
*唯一标识：d10cc75d-4692-46d4-946c-222df3b6f05a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/8 15:38:33
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/8 15:38:33
*修改人： yswenli
*版本号： V3.3.3.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.QueueSocket.Net;
using SAEA.QueueSocket.Type;
using System.Collections.Generic;
using System.Threading;

namespace SAEA.QueueSocket.Model
{
    /// <summary>
    /// 队列编解码器
    /// </summary>
    public class QueueCoder
    {
        AutoResetEvent _autoResetEvent = new AutoResetEvent(false);

        /// <summary>
        /// 按指定格式编码
        /// </summary>
        /// <param name="cmdType"></param>
        /// <param name="contents"></param>
        /// <returns></returns>
        public byte[] Encode(QueueSocketMsgType cmdType, string name, string topic, string data)
        {
            return QUnpacker.Encode(new QueueSocketMsg(cmdType, name, topic, data));
        }
        /// <summary>
        /// 按指定格式编码批量处理
        /// </summary>
        /// <param name="cmdType"></param>
        /// <param name="name"></param>
        /// <param name="topic"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public byte[] Encode(QueueSocketMsgType cmdType, string name, string topic, string[] data)
        {
            List<byte> list = new List<byte>();
            if (data != null)
            {
                foreach (var item in data)
                {
                    list.AddRange(Encode(cmdType, name, topic, data));
                }
            }
            return list.ToArray();
        }

        public byte[] Ping(string name)
        {
            return Encode(QueueSocketMsgType.Ping, name, string.Empty, string.Empty);
        }

        public byte[] Pong(string name)
        {
            return Encode(QueueSocketMsgType.Pong, name, string.Empty, DateTimeHelper.ToString());
        }

        public byte[] Publish(string name, string topic, string data)
        {
            return Encode(QueueSocketMsgType.Publish, name, topic, data);
        }

        public byte[] Subscribe(string name, string topic)
        {
            return Encode(QueueSocketMsgType.Subcribe, name, topic, string.Empty);
        }

        public byte[] Unsubcribe(string name, string topic)
        {
            return Encode(QueueSocketMsgType.Unsubcribe, name, topic, string.Empty);
        }

        public byte[] Close(string name)
        {
            return Encode(QueueSocketMsgType.Close, name, string.Empty, string.Empty);
        }

        public byte[] Data(string name, string topic, string data)
        {
            return Encode(QueueSocketMsgType.Data, name, topic, data);
        }


    }
}
