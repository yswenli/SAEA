﻿/****************************************************************************
*项目名称：SAEA.FTP.Net
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTP.Net
*类 名 称：FUnpacker
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/7 15:01:28
*描述：
*=====================================================================
*修改时间：2019/11/7 15:01:28
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.Sockets.Interface;

using System;
using System.Collections.Generic;

namespace SAEA.FTP.Net
{
    public class FTPCoder : ICoder
    {

        public byte[] Encode(ISocketProtocal protocal)
        {
            return protocal.ToBytes();
        }

        public void Decode(byte[] data, Action<ISocketProtocal> unpackCallback, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {

        }
        public void Clear()
        {
            throw new NotImplementedException();
        }

        public List<ISocketProtocal> Decode(byte[] data, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            throw new NotImplementedException();
        }
    }
}
