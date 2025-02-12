/****************************************************************************
*项目名称：SAEA.Http.Base.Net
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Http.Base.Net
*类 名 称：HttpSocketDebug
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/7/31 10:05:46
*描述：
*=====================================================================
*修改时间：2020/7/31 10:05:46
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
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


        public HttpSocketDebug(int port, int bufferSize = 1024 * 10, int count = 10000, int timeOut = 180 * 1000)
        {
            var optionBuilder = new SocketOptionBuilder()
               .SetSocket(SAEASocketType.Tcp)
               .UseIocp<HttpCoder>()
               .SetPort(port)
               .SetCount(count)
               .SetReadBufferSize(bufferSize)
               .SetTimeOut(timeOut)
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
