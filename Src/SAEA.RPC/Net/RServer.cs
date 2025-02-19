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
        /// 收到消息事件
        /// </summary>
        public event Action<IUserToken, RSocketMsg> OnMsg;

        /// <summary>
        /// 错误事件
        /// </summary>
        public event OnErrorHandler OnError;

        /// <summary>
        /// 构造函数，初始化RServer实例
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="maxConnects">最大连接数</param>
        public RServer(int port = 39654, int bufferSize = 64 * 1024, int maxConnects = 1000)
        {
            var option = SocketOptionBuilder.Instance
                .SetSocket()
                .UseIocp<RpcCoder>()
                .SetPort(port)
                .SetReadBufferSize(bufferSize)
                .SetWriteBufferSize(bufferSize)
                .SetMaxConnects(maxConnects)
                .Build();

            _server = SocketFactory.CreateServerSocket(option);

            _server.OnError += _server_OnError;

            _server.OnReceive += _server_OnReceive;

        }

        /// <summary>
        /// 错误处理方法
        /// </summary>
        /// <param name="ID">会话ID</param>
        /// <param name="ex">异常对象</param>
        private void _server_OnError(string ID, Exception ex)
        {
            OnError?.Invoke(ID, ex);
        }

        /// <summary>
        /// 接收数据处理方法
        /// </summary>
        /// <param name="currentObj">当前对象</param>
        /// <param name="data">接收到的数据</param>
        private void _server_OnReceive(object currentObj, byte[] data)
        {
            var userToken = (IUserToken)currentObj;

            ((RpcCoder)(userToken.Coder)).Unpack(data, (r) =>
            {
                OnMsg.Invoke(userToken, r);
            });
        }

        /// <summary>
        /// 回复消息
        /// </summary>
        /// <param name="userToken">用户令牌</param>
        /// <param name="msg">消息对象</param>
        internal void Reply(IUserToken userToken, RSocketMsg msg)
        {
            var data = ((RpcCoder)userToken.Coder).Encode(msg);
            _server.SendAsync(userToken.ID, data);
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="userToken">用户令牌</param>
        internal void Disconnect(IUserToken userToken)
        {
            _server.Disconnect(userToken.ID);
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        internal void Start()
        {
            _server.Start();
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        internal void Stop()
        {
            _server.Stop();
        }
    }
}
