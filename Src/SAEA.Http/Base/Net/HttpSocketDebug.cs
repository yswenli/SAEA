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
*文件名： HttpSocketDebug
*版本号： v26.4.23.1
*唯一标识：5da08cf9-7d30-43aa-a296-e89cea16f0c6
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/07/31 10:13:22
*描述：HttpSocketDebug类
*
*=====================================================================
*修改标记
*修改时间：2020/07/31 10:13:22
*修改人： yswenli
*版本号： v26.4.23.1
*描述：HttpSocketDebug类
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

using System;

namespace SAEA.Http.Base.Net
{
    class HttpSocketDebug : IHttpSocket
    {
        IServerSocket _serverSokcet;

        ISocketOption _option;

        public event Action<IUserToken, HttpMessage> OnRequested;

        public event Action<Exception> OnError;


        public HttpSocketDebug(int port,
            int bufferSize = 1024 * 64,
            int maxConnects = 1000,
            double timeout = 180,
            double connectTimeout = 2)
        {
            var optionBuilder = new SocketOptionBuilder()
               .SetSocket(SAEASocketType.Tcp)
               .UseIocp<HttpCoder>()
               .SetPort(port)
               .SetMaxConnects(maxConnects)
               .SetReadBufferSize(bufferSize)
               .SetActionTimeOut((int)(timeout * 1000))
               .SetConnectTimeout((int)(connectTimeout * 1000))
               .ReusePort(false);
            _option = optionBuilder.Build();

            _serverSokcet = SocketFactory.CreateServerSocket(_option);
            _serverSokcet.OnReceive += _serverSokcet_OnReceive;
            _serverSokcet.OnError += (i, e) => OnError?.Invoke(e);
        }

        private void _serverSokcet_OnReceive(object userToken, byte[] data)
        {
            LogHelper.Debug(userToken == null ? "userToken is null" : "userToken is not null");

            LogHelper.Debug("HttpSocket.Recieve", data);

            var ut = (IUserToken)userToken;

            try
            {
                if (ut == null) throw new KernelException("userToken is null");
                HttpCoder unpacker = (HttpCoder)ut.Coder;
                var msgs = unpacker.GetRequest(ut.ID, data);
                if (msgs == null || msgs.Count < 1) return;
                foreach (var msg in msgs)
                {
                    OnRequested?.Invoke(ut, msg);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("Http解码出现异常", ex);
                Disconnecte(ut);
            }
        }

        public void Send(IUserToken userToken, byte[] data)
        {
            _serverSokcet.Send(userToken.ID, data);
        }

        public void Disconnecte(IUserToken userToken)
        {
            _serverSokcet.Disconnect(userToken.ID);
        }
        public void End(IUserToken userToken, byte[] data)
        {
            _serverSokcet.End(userToken.ID, data);
        }
        public void Start()
        {
            _serverSokcet.Start();
        }

        public void Stop()
        {
            _serverSokcet.Stop();
        }
    }
}
