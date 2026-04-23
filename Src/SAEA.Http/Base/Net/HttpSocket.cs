/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Base.Net
*文件名： HttpSocket
*版本号： v26.4.23.1
*唯一标识：914e5704-953b-48b7-825d-256de2c54f24
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/30 03:46:59
*描述：HttpSocket套接字类
*
*=====================================================================
*修改标记
*修改时间：2018/10/30 03:46:59
*修改人： yswenli
*版本号： v26.4.23.1
*描述：HttpSocket套接字类
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
        /// <param name="port"></param>
        /// <param name="bufferSize"></param>
        /// <param name="maxConnects"></param>
        /// <param name="timeout"></param>
        /// <param name="connectTimeout"></param>
        public HttpSocket(int port,
            int bufferSize = 64 * 1024,
            int maxConnects = 1000,
            double timeout = 180,
            double connectTimeout = 3)
        {
            var optionBuilder = new SocketOptionBuilder()
               .SetSocket(Sockets.Model.SAEASocketType.Tcp)
               .UseIocp<HttpCoder>()
               .SetPort(port)
               .SetMaxConnects(maxConnects)
               .SetReadBufferSize(bufferSize)
               .SetActionTimeOut((int)(timeout * 1000))
               .SetFreeTime((int)(timeout * 1000))
               .SetConnectTimeout((int)(connectTimeout * 1000))
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