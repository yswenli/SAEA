/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Base.Net
*文件名： RClient
*版本号： v4.3.3.7
*唯一标识：a22caf84-4c61-456e-98cc-cbb6cb2c6d6e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/11/5 20:45:02
*描述：
*
*=====================================================================
*修改标记
*创建时间：2018/11/5 20:45:02
*修改人： yswenli
*版本号： v4.3.3.7
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Core.Tcp;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Base.Net
{
    internal class RClient : IocpClientSocket
    {
        public event Action<string> OnMessage;

        public event Action<DateTime> OnActived;

        Queue<byte[]> queue = new Queue<byte[]>();


        public RClient(int bufferSize = 100 * 1024, string ip = "127.0.0.1", int port = 39654) : base(new RContext(), ip, port, bufferSize)
        {
            TaskHelper.Start(() =>
            {
                while (true)
                {
                    if (queue.Count > 0)
                    {
                        var data = queue.Dequeue();

                        this.UserToken.Unpacker.Unpack(data, (content) =>
                        {
                            OnMessage.Invoke(Encoding.UTF8.GetString(content.Content));
                        }, null, null);
                    }
                    else
                    {
                        ThreadHelper.Sleep(1);
                    }
                }
            });
        }


        protected override void OnReceived(byte[] data)
        {
            queue.Enqueue(data);
        }


        public void Request(byte[] cmd)
        {
            BeginSend(cmd);
            OnActived.Invoke(DateTimeHelper.Now);
        }
    }
}
