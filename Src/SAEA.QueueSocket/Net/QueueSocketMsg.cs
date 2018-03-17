/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.QueueSocket.Net
*文件名： QueueSocketMsg
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/

using SAEA.Commom;
using SAEA.QueueSocket.Type;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.QueueSocket.Net
{
    public class QueueSocketMsg : ISocketProtocal, IDisposable
    {
        public long BodyLength
        {
            get; set;
        }
        public byte Type
        {
            get; set;
        }
        public Byte[] Content
        {
            get; set;
        }

        public byte[] ToBytes()
        {
            var len = ((int)BodyLength).ToBytes();

            var data = new List<byte>();

            data.AddRange(len);

            data.Add(Type);

            if (Content != null)
            {
                data.AddRange(Content);
            }

            return data.ToArray();
        }

        public static QueueSocketMsg Parse(byte[] data, QueueSocketMsgType type)
        {
            var msg = new QueueSocketMsg();

            if (data != null)
                msg.BodyLength = data.Length;
            else
                msg.BodyLength = 0;

            msg.Type = (byte)type;

            if (msg.BodyLength > 0)
            {
                msg.Content = data;
            }

            return msg;
        }

        public void Dispose()
        {
            if (this.Content != null)
            {
                this.Content = null;
            }
        }

    }
}
