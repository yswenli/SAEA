/****************************************************************************
*项目名称：SAEA.Sockets.TcpTest
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Sockets.TcpTest
*类 名 称：JServer
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/5 10:55:08
*描述：
*=====================================================================
*修改时间：2021/1/5 10:55:08
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System;

using JT808.Protocol;

using SAEA.Sockets.Interface;
using SAEA.Sockets.Shortcut;

namespace SAEA.Sockets.TcpTest
{
    public class JServer
    {
        TCPServer<JUnpacker> _server;

        public event Action<JServer, string, JT808Package> OnReceive;

        public JServer()
        {
            _server = new TCPServer<JUnpacker>(39808);

            _server.OnAccept += _server_OnAccept;
            _server.OnDisconnect += _server_OnDisconnect;
            _server.OnError += _server_OnError;
            _server.OnReceive += _server_OnReceive;
        }

        private void _server_OnReceive(TCPServer<JUnpacker> arg1, IUserToken arg2, byte[] arg3)
        {
            var jUnpacker = (JUnpacker)arg2.Coder;

            jUnpacker.DeCode(arg3, (b) =>
            {
                var package = new JT808Serializer().Deserialize<JT808Package>(b.AsSpan());
                OnReceive?.Invoke(this, arg2.ID, package);
            });
        }

        private void _server_OnError(TCPServer<JUnpacker> arg1, string arg2, Exception arg3)
        {
            Console.WriteLine($"_server_OnError:{arg3.Message}");
        }

        private void _server_OnDisconnect(TCPServer<JUnpacker> arg1, string arg2, Exception arg3)
        {
            Console.WriteLine($"ServerSokcet_OnDisconnected:{arg2}");
        }

        private void _server_OnAccept(TCPServer<JUnpacker> arg1, string arg2)
        {
            Console.WriteLine($"ServerSokcet_OnAccepted:{arg2}");
        }

        public void Start()
        {
            _server.Start();
        }

        public void SendAsync(string id, JT808Package jT808Package)
        {
            _server.SendAsync(id, new JT808Serializer().Serialize(jT808Package));
        }
    }
}
