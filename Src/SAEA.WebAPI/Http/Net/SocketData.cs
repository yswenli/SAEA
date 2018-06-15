/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.WebAPI.Http.Net
*文件名： SocketData
*版本号： V1.0.0.0
*唯一标识：1d9bbeb9-0d69-47e9-bf97-ce4cb11789d0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/8 17:50:13
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/8 17:50:13
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.WebAPI.Http.Net
{
    public class SocketData : ISocketProtocal
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

            //data.AddRange(len);

            //data.Add(Type);

            if (Content != null)
            {
                data.AddRange(Content);
            }

            return data.ToArray();
        }

        public static SocketData Parse(byte[] data)
        {
            var msg = new SocketData();

            if (data != null)
                msg.BodyLength = data.Length;
            else
                msg.BodyLength = 0;

            if (msg.BodyLength > 0)
            {
                msg.Content = data;
            }

            return msg;
        }
    }
}
