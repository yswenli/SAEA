/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Base.Net
*文件名： HttpSocket
*版本号： v5.0.0.1
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
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;

namespace SAEA.Http.Base.Net
{
    class HttpSocket
    {
        IServerSokcet _serverSokcet;

        public event Action<IUserToken, HttpMessage> OnRequested;

        /// <summary>
        /// 测试模式
        /// </summary>
        public bool IsDebug
        {
            get; set;
        }


        public HttpSocket(int port, int bufferSize = 1024 * 10, int count = 10000, int timeOut = 120 * 1000, bool isDebug = false)
        {
            var optionBuilder = new SocketOptionBuilder()
               .SetSocket(Sockets.Model.SocketType.Tcp)
               .UseIocp(new HContext())
               .SetPort(port)
               .SetCount(count)
               .SetBufferSize(bufferSize)
               .SetTimeOut(timeOut);
            var option = optionBuilder.Build();

            _serverSokcet = SocketFactory.CreateServerSocket(option);
            _serverSokcet.OnReceive += _serverSokcet_OnReceive;

            IsDebug = isDebug;
        }

        private void _serverSokcet_OnReceive(object userToken, byte[] data)
        {
            if (IsDebug)
            {
                LogHelper.Debug(userToken == null ? "userToken is null" : "userToken is not null");
                LogHelper.Debug(data);
            }

            var ut = (IUserToken)userToken;

            try
            {
                if (ut == null) throw new KernelException("userToken is null");

                HUnpacker unpacker = (HUnpacker)ut.Unpacker;

                unpacker.GetRequest(data, (result) =>
                {
                    OnRequested?.Invoke(ut, result);
                });

            }
            catch (Exception ex)
            {
                LogHelper.Error("Http解码出现异常", ex);
                Disconnecte(ut);
            }
        }

        public void End(IUserToken userToken, byte[] data)
        {
            ((IocpServerSocket)_serverSokcet).End(userToken, data);
        }

        public void Disconnecte(IUserToken userToken)
        {
            _serverSokcet.Disconnecte(userToken);
        }

        public void Start()
        {
            _serverSokcet.Start();
        }

        public void Stop()
        {
            _serverSokcet.Dispose();
        }
    }
}
