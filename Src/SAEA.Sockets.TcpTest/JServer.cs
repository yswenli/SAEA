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
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using JT808.Protocol;
using JT808.Protocol.MessageBody;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.Sockets.TcpTest
{
    public class JServer
    {
        IServerSocket _serverSokcet;        

        public event Action<JServer, string, JT808Package> OnReceive;

        public JServer()
        {

            //tcpserver
            _serverSokcet = SocketFactory.CreateServerSocket(SocketOptionBuilder.Instance
                .SetSocket(Model.SAEASocketType.Tcp)
                .SetPort(39808)
                .UseIocp<JContext>()
                .Build());

            _serverSokcet.OnAccepted += ServerSokcet_OnAccepted;
            _serverSokcet.OnDisconnected += ServerSokcet_OnDisconnected;
            _serverSokcet.OnError += ServerSokcet_OnError;
            _serverSokcet.OnReceive += ServerSokcet_OnReceive;
        }

        public void Start()
        {
            _serverSokcet.Start();
        }

        public void SendAsync(string id, JT808Package jT808Package)
        {
            _serverSokcet.SendAsync(id, new JT808Serializer().Serialize(jT808Package));
        }

        private void ServerSokcet_OnReceive(Interface.ISession currentSession, byte[] data)
        {
            IUserToken userToken = (IUserToken)currentSession;
            var jUnpacker = (JUnpacker)userToken.Unpacker;

            jUnpacker.DeCode(data, (b) =>
            {
                var package = new JT808Serializer().Deserialize<JT808Package>(b.AsSpan());
                OnReceive?.Invoke(this, userToken.ID, package);
            });
        }

        private void ServerSokcet_OnError(string ID, Exception ex)
        {
            Console.WriteLine($"ServerSokcet_OnDisconnected:{ex.Message}");
        }

        private void ServerSokcet_OnDisconnected(string ID, Exception ex)
        {
            Console.WriteLine($"ServerSokcet_OnDisconnected:{ex.Message}");
        }

        private void ServerSokcet_OnAccepted(object obj)
        {
            Console.WriteLine($"ServerSokcet_OnAccepted:{((IUserToken)obj).ID}");
        }



    }
}
