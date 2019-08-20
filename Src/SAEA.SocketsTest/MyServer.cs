/****************************************************************************
*项目名称：SAEA.SocketsTest
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.SocketsTest
*类 名 称：MyServerSocket
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/8/19 15:03:45
*描述：
*=====================================================================
*修改时间：2019/8/19 15:03:45
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using JT808.Protocol;
using JT808.Protocol.Enums;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using System;

namespace SAEA.SocketsTest
{
    class MyServer
    {
        IServerSokcet _server;

        public MyServer()
        {
            var option = SocketOptionBuilder.Instance.UseIocp(new MyContext())
                .SetPort(39654)
                .Build();

            _server = SocketFactory.CreateServerSocket(option);

            _server.OnReceive += _server_OnReceive;
        }

        public event Action<JT808Package> OnReceived;

        private void _server_OnReceive(object userToken, byte[] data)
        {
            var ut = (IUserToken)userToken;

            var up = (MyUnpacker)ut.Unpacker;

            up.Unpack(data, (package) =>
            {
                OnReceived?.Invoke(package);

                _server.SendAsync(ut.ID, up.Process(package));
            });
        }


        public void Start()
        {
            _server.Start();
        }
    }
}
