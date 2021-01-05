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
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using JT808.Protocol;
using JT808.Protocol.MessageBody;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.Sockets.TcpTest
{
    public class JClient
    {
        JUnpacker _jUnpacker;

        IClientSocket _clientSokcet;

        public event Action<JClient, JT808Package> OnReceive;

        public JClient()
        {
            _jUnpacker = new JUnpacker();

            _clientSokcet = SocketFactory.CreateClientSocket(SocketOptionBuilder.Instance
               .SetSocket(Model.SAEASocketType.Tcp)
               .SetIPEndPoint(new System.Net.IPEndPoint(System.Net.IPAddress.Parse("127.0.0.1"), 39808))
               .UseIocp<JContext>()
               .Build());

            _clientSokcet.OnReceive += ClientSokcet_OnReceive;
            _clientSokcet.OnDisconnected += ClientSokcet_OnDisconnected;
            _clientSokcet.OnError += ClientSokcet_OnError;
        }

        public void Connect()
        {
            _clientSokcet.ConnectAsync();
        }

        public void SendAsync(JT808Package jT808Package)
        {
            _clientSokcet.SendAsync(new JT808Serializer().Serialize(jT808Package));
        }


        private void ClientSokcet_OnError(string ID, Exception ex)
        {
            Console.WriteLine($"ClientSokcet_OnError:{ex.Message}");
        }

        private void ClientSokcet_OnDisconnected(string ID, Exception ex)
        {
            Console.WriteLine($"ClientSokcet_OnDisconnected:{ex.Message}");
        }

        private void ClientSokcet_OnReceive(byte[] data)
        {
            _jUnpacker.DeCode(data, (b) =>
            {
                var package = new JT808Serializer().Deserialize<JT808Package>(b.AsSpan());
                OnReceive.Invoke(this, package);
            });
        }
    }
}
