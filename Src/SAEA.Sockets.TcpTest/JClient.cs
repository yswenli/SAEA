/****************************************************************************
*项目名称：SAEA.Sockets.TcpTest
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Sockets.TcpTest
*类 名 称：JClient
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/5 10:55:21
*描述：
*=====================================================================
*修改时间：2021/1/5 10:55:21
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System;

using JT808.Protocol;

using SAEA.Sockets.Shortcut;

namespace SAEA.Sockets.TcpTest
{
    public class JClient
    {
        JUnpacker _jUnpacker;

        TCPClient<JUnpacker> _client;

        public event Action<JClient, JT808Package> OnReceive;

        public JClient()
        {
            _jUnpacker = new JUnpacker();

            _client = new TCPClient<JUnpacker>("127.0.0.1", 39808);

            _client.OnError += _client_OnError;

            _client.OnDisconnect += _client_OnDisconnect;

            _client.OnReceive += _client_OnReceive;
        }

        private void _client_OnReceive(TCPClient<JUnpacker> arg1, byte[] arg2)
        {
            var b = _jUnpacker.Decode(arg2);
            if (b == null) return;
            var package = new JT808Serializer().Deserialize<JT808Package>(b.AsSpan());
            OnReceive.Invoke(this, package);
        }

        private void _client_OnDisconnect(TCPClient<JUnpacker> arg1, Exception arg2)
        {
            Console.WriteLine($"ClientSokcet_OnDisconnected:{arg1}");
        }

        private void _client_OnError(TCPClient<JUnpacker> arg1, Exception arg2)
        {
            Console.WriteLine($"ClientSokcet_OnError:{arg2.Message}");
        }

        public void Connect()
        {
            _client.Connect();
        }

        public void Disconnect()
        {
            _client.Disconnect();
        }

        public void SendAsync(JT808Package jT808Package)
        {
            _client.SendAsync(new JT808Serializer().Serialize(jT808Package));
        }
    }
}
