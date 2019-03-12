/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.TcpP2P.Net
*文件名： QSocketMsg
*版本号： v4.2.3.1
*唯一标识：8619d735-c009-4982-ab82-1eea057eb195
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 21:35:35
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 21:35:35
*修改人： yswenli
*版本号： v4.2.3.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.TcpP2P.Net
{
    public class PSocketMsg : ISocketProtocal
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
            var len = BodyLength.ToBytes();

            var data = new List<byte>();

            data.AddRange(len);

            data.Add(Type);

            if (Content != null)
            {
                data.AddRange(Content);
            }

            return data.ToArray();
        }

        public static PSocketMsg Parse(byte[] data, TcpP2pType type)
        {
            var msg = new PSocketMsg();

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
    }
}
