/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPC.Net
*文件名： RServer
*版本号： v7.0.0.1
*唯一标识：5732cac7-ae47-4e6c-8533-844e350f3f81
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/16 16:16:54
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/16 16:16:54
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using System;

using SAEA.Sockets;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;

namespace SAEA.RPC.Net
{
    /// <summary>
    /// provider socket处理
    /// </summary>
    internal class RServer
    {
        IServerSocket _server;

        /// <summary>
        /// 收到消息
        /// </summary>
        public event Action<IUserToken, RSocketMsg> OnMsg;

        public event OnErrorHandler OnError;

        public RServer(int port = 39654, int bufferSize = 100 * 1024, int count = 10000)
        {
            var option = SocketOptionBuilder.Instance
                .SetSocket()
                .UseIocp<RpcCoder>()
                .SetPort(port)
                .SetReadBufferSize(bufferSize)
                .SetWriteBufferSize(bufferSize)
                .SetMaxConnects(count)
                .Build();

            _server = SocketFactory.CreateServerSocket(option);

            _server.OnError += _server_OnError;

            _server.OnReceive += _server_OnReceive;

        }

        private void _server_OnError(string ID, Exception ex)
        {
            OnError?.Invoke(ID, ex);
        }

        private void _server_OnReceive(object currentObj, byte[] data)
        {
            var userToken = (IUserToken)currentObj;

            ((RpcCoder)(userToken.Coder)).Unpack(data, (r) =>
            {
                OnMsg.Invoke(userToken, r);
            });
        }
        /// <summary>
        /// 回复
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="msg"></param>
        internal void Reply(IUserToken userToken, RSocketMsg msg)
        {
            var data = ((RpcCoder)userToken.Coder).Encode(msg);
            _server.SendAsync(userToken.ID, data);
        }

        internal void Disconnect(IUserToken userToken)
        {
            _server.Disconnect(userToken.ID);
        }

        internal void Start()
        {
            _server.Start();
        }

        internal void Stop()
        {
            _server.Stop();
        }
    }
}
