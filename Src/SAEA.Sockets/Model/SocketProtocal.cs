/****************************************************************************
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets
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

using SAEA.Common;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;

namespace SAEA.Sockets.Model
{
    /// <summary>
    /// 系统默认消息协议
    /// </summary>
    public class SocketProtocal : ISocketProtocal
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

        public static SocketProtocal Parse(byte[] data, SocketProtocalType type)
        {
            var msg = new SocketProtocal();

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

        public static SocketProtocal ParseRequest(byte[] data)
        {
            return Parse(data, SocketProtocalType.RequestSend);
        }


        public static SocketProtocal ParseStream(byte[] data)
        {
            var msg = new SocketProtocal();

            if (data != null)
                msg.BodyLength = data.Length;
            else
                msg.BodyLength = 0;

            msg.Type = (byte)SocketProtocalType.BigData;

            if (msg.BodyLength > 0)
            {
                msg.Content = data;
            }

            return msg;
        }

    }
}
