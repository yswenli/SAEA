/****************************************************************************
*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Base.Net
*文件名： HttpSocket
*版本号： v6.0.0.1
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
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/
using System;

using SAEA.Common;
using SAEA.Sockets;
using SAEA.Sockets.Interface;

namespace SAEA.Http.Base.Net
{
    class HttpSocket : IHttpSocket
    {
        IServerSocket _serverSokcet;

        ISocketOption _option;

        public event Action<IUserToken, HttpMessage> OnRequested;

        public event Action<Exception> OnError;

        public HttpSocket(int port, int bufferSize = 1024 * 10, int count = 10000, int timeOut = 120 * 1000)
        {
            var optionBuilder = new SocketOptionBuilder()
               .SetSocket(Sockets.Model.SAEASocketType.Tcp)
               .UseIocp<HUnpacker>()
               .SetPort(port)
               .SetCount(count)
               .SetReadBufferSize(bufferSize)
               .SetTimeOut(timeOut)
               .SetFreeTime(timeOut)
               .ReusePort(false);

            _option = optionBuilder.Build();

            _serverSokcet = SocketFactory.CreateServerSocket(_option);
            _serverSokcet.OnReceive += _serverSokcet_OnReceive;
            _serverSokcet.OnError += _serverSokcet_OnError;
        }

        private void _serverSokcet_OnReceive(object userToken, byte[] data)
        {
            var ut = (IUserToken)userToken;

            HUnpacker unpacker = (HUnpacker)ut.Unpacker;

            try
            {
                unpacker.GetRequest(ut.ID, data, (result) =>
                {
                    OnRequested?.Invoke(ut, result);
                });
            }
            catch (Exception ex)
            {
                unpacker.Clear();
                LogHelper.Error("Http解码出现异常", ex, Convert.ToBase64String(data));
                Disconnecte(ut);
            }
        }

        private void _serverSokcet_OnError(string ID, Exception ex)
        {
            OnError?.Invoke(ex);
        }

        public void Send(IUserToken userToken, byte[] data)
        {
            _serverSokcet.SendAsync(userToken.ID, data);
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
