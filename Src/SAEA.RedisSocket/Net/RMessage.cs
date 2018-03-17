/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RedisSocket.Net
*文件名： RMessage
*版本号： V1.0.0.0
*唯一标识：80182c35-16a9-4e5a-ae2d-d0055003865f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/16 10:44:14
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/16 10:44:14
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Net
{
    class RMessage : ISocketProtocal
    {
        public long BodyLength { get; set; }
        public byte[] Content { get; set; }
        public byte Type { get; set; }

        public byte[] ToBytes()
        {
            return this.Content;
        }
    }
}
