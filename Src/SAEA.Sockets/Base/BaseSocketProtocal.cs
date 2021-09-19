/****************************************************************************
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets
*文件名： SocketProtocal
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
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;
using System.Collections.Generic;

namespace SAEA.Sockets.Base
{
    /// <summary>
    /// 系统默认消息协议
    /// </summary>
    public class BaseSocketProtocal : ISocketProtocal
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

        public static BaseSocketProtocal Parse(byte[] data, SocketProtocalType type)
        {
            return Parse(data, (byte)type);
        }


        public static BaseSocketProtocal Parse(byte[] data, byte type)
        {
            var msg = new BaseSocketProtocal();

            if (data != null)
                msg.BodyLength = data.Length;
            else
                msg.BodyLength = 0;

            msg.Type = type;

            if (msg.BodyLength > 0)
            {
                msg.Content = data;
            }

            return msg;
        }


        public static BaseSocketProtocal ParseRequest(byte[] data)
        {
            return Parse(data, SocketProtocalType.RequestSend);
        }


        public static BaseSocketProtocal ParseStream(byte[] data)
        {
            var msg = new BaseSocketProtocal();

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
