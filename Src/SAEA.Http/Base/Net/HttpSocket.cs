/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Base.Net
*文件名： HttpSocket
*版本号： v7.0.0.1
*唯一标识：ab912b9a-c7ed-44d9-8e48-eef0b6ff86a2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/8 17:11:15
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/8 17:11:15
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using System;

using SAEA.Common;
using SAEA.Sockets;
using SAEA.Sockets.Interface;

namespace SAEA.Http.Base.Net
{
    /// <summary>
    /// HttpSocket类，实现IHttpSocket接口
    /// </summary>
    class HttpSocket : IHttpSocket
    {
        IServerSocket _serverSokcet;

        ISocketOption _option;

        /// <summary>
        /// 请求事件
        /// </summary>
        public event Action<IUserToken, HttpMessage> OnRequested;

        /// <summary>
        /// 错误事件
        /// </summary>
        public event Action<Exception> OnError;

        /// <summary>
        /// HttpSocket构造函数
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="maxConnect">连接数</param>
        /// <param name="timeOut">超时时间</param>
        public HttpSocket(int port, int bufferSize = 64 * 1024, int maxConnect = 1000, int timeOut = 180 * 1000)
        {
            var optionBuilder = new SocketOptionBuilder()
               .SetSocket(Sockets.Model.SAEASocketType.Tcp)
               .UseIocp<HttpCoder>()
               .SetPort(port)
               .SetMaxConnects(maxConnect)
               .SetReadBufferSize(bufferSize)
               .SetTimeOut(timeOut)
               .SetFreeTime(timeOut)
               .ReusePort(false);

            _option = optionBuilder.Build();

            _serverSokcet = SocketFactory.CreateServerSocket(_option);
            _serverSokcet.OnReceive += _serverSokcet_OnReceive;
            _serverSokcet.OnError += _serverSokcet_OnError;
        }

        /// <summary>
        /// 接收数据事件处理
        /// </summary>
        /// <param name="userToken">用户令牌</param>
        /// <param name="data">接收的数据</param>
        private void _serverSokcet_OnReceive(object userToken, byte[] data)
        {
            var ut = (IUserToken)userToken;

            HttpCoder unpacker = (HttpCoder)ut.Coder;

            try
            {
                var msgs = unpacker.GetRequest(ut.ID, data);
                if (msgs == null || msgs.Count < 1) return;
                foreach (var msg in msgs)
                {
                    OnRequested?.Invoke(ut, msg);
                }
            }
            catch (Exception ex)
            {
                unpacker.Clear();
                LogHelper.Error("Http解码出现异常", ex, Convert.ToBase64String(data));
                Disconnecte(ut);
            }
        }

        /// <summary>
        /// 错误事件处理
        /// </summary>
        /// <param name="ID">会话ID</param>
        /// <param name="ex">异常信息</param>
        private void _serverSokcet_OnError(string ID, Exception ex)
        {
            OnError?.Invoke(ex);
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="userToken">用户令牌</param>
        /// <param name="data">发送的数据</param>
        public void Send(IUserToken userToken, byte[] data)
        {
            _serverSokcet.SendAsync(userToken.ID, data);
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="userToken">用户令牌</param>
        public void Disconnecte(IUserToken userToken)
        {
            _serverSokcet.Disconnect(userToken.ID);
        }

        /// <summary>
        /// 结束会话
        /// </summary>
        /// <param name="userToken">用户令牌</param>
        /// <param name="data">结束的数据</param>
        public void End(IUserToken userToken, byte[] data)
        {
            _serverSokcet.End(userToken.ID, data);
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        public void Start()
        {
            _serverSokcet.Start();
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public void Stop()
        {
            _serverSokcet.Stop();
        }
    }
}
