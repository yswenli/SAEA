/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RedisSocket
*文件名： RedisClient
*版本号： V1.0.0.0
*唯一标识：5b29d71e-6b9a-4379-8280-0a0e5cc66708
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/16 9:29:15
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/16 9:29:15
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Sockets.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Net
{
    /// <summary>
    /// redis socket 连接类
    /// </summary>
    public class RConnection : BaseClientSocket
    {
        public event Action<string> OnMessage;

        public RConnection(int bufferSize = 100 * 1024, string ip = "127.0.0.1", int port = 39654) : base(new RContext(), ip, port, bufferSize)
        {
        }

        public event Action<DateTime> OnActived;

        protected override void OnReceived(byte[] data)
        {
            OnActived?.Invoke(DateTimeHelper.Now.AddSeconds(30));
            if (data != null)
            {
                this.UserToken.Coder.Pack(data, null, (content) =>
                {
                    OnMessage?.Invoke(Encoding.UTF8.GetString(content.Content));
                }, null);

            }
        }

        public void Send(string cmd)
        {
            Send(Encoding.UTF8.GetBytes(cmd));
            OnActived?.Invoke(DateTimeHelper.Now.AddSeconds(30));
        }
    }
}
