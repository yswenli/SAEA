/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.TcpP2P.Net
*文件名： QUserToken
*版本号： V1.0.0.0
*唯一标识：24ba2e50-2c22-40b0-870b-4b510d555099
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 21:30:34
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 21:30:34
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SAEA.TcpP2P.Net
{
    public class PUserToken : IUserToken
    {
        AutoResetEvent _autoResetEvent = new AutoResetEvent(true);

        public string ID
        {
            get; set;
        }
        public Socket Socket
        {
            get; set;
        }

        public byte[] Buffer
        {
            get; set;
        }

        public DateTime Linked
        {
            get; set;
        }

        public DateTime Actived
        {
            get; set;
        }

        public ICoder Coder
        {
            get; set;
        }

        public void WaitOne()
        {
            _autoResetEvent.WaitOne();
        }


        public void Set()
        {
            _autoResetEvent.Set();
        }

        public void Dispose()
        {
            _autoResetEvent.Close();
            if (Buffer != null)
                Array.Clear(Buffer, 0, Buffer.Length);
            Socket?.Close();
        }
    }
}
