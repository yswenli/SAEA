using JT808.Protocol;
using JT808.Protocol.MessageBody;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;

namespace SAEA.Sockets.TcpTest
{
    class Program
    {
        static IServerSokcet _serverSokcet;

        static IClientSocket _clientSokcet;
        static JUnpacker _jUnpacker;

        static void Main(string[] args)
        {
            Console.Title = "SAEA.Sockets.TcpTest JT808";

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

            _serverSokcet.Start();


            //tcp client
            _jUnpacker = new JUnpacker();

            _clientSokcet = SocketFactory.CreateClientSocket(SocketOptionBuilder.Instance
               .SetSocket(Model.SAEASocketType.Tcp)
               .SetIPEndPoint(new System.Net.IPEndPoint(System.Net.IPAddress.Parse("127.0.0.1"), 39808))
               .UseIocp<JContext>()
               .Build());

            _clientSokcet.OnReceive += ClientSokcet_OnReceive;
            _clientSokcet.OnDisconnected += ClientSokcet_OnDisconnected;
            _clientSokcet.OnError += ClientSokcet_OnError;

            _clientSokcet.ConnectAsync();

            var data = GetJT808PositionData();
            _clientSokcet.SendAsync(data);

            Console.ReadLine();
        }

        /// <summary>
        /// 位置信息汇报
        /// </summary>
        /// <returns></returns>
        static byte[] GetJT808PositionData()
        {
            JT808Package jT808Package = new JT808Package();

            jT808Package.Header = new JT808Header
            {
                MsgId = (ushort)JT808.Protocol.Enums.JT808MsgId.位置信息汇报,
                ManualMsgNum = 0,
                TerminalPhoneNo = "123456789012"
            };

            JT808_0x0200 jT808_0x0200 = new JT808_0x0200();
            jT808_0x0200.AlarmFlag = 1;
            jT808_0x0200.Altitude = 40;
            jT808_0x0200.GPSTime = DateTime.Parse("2018-10-15 10:10:10");
            jT808_0x0200.Lat = 12222222;
            jT808_0x0200.Lng = 132444444;
            jT808_0x0200.Speed = 60;
            jT808_0x0200.Direction = 0;
            jT808_0x0200.StatusFlag = 2;
            jT808_0x0200.JT808LocationAttachData = new Dictionary<byte, JT808_0x0200_BodyBase>();

            jT808_0x0200.JT808LocationAttachData.Add(JT808Constants.JT808_0x0200_0x01, new JT808_0x0200_0x01
            {
                Mileage = 100
            });

            jT808_0x0200.JT808LocationAttachData.Add(JT808Constants.JT808_0x0200_0x02, new JT808_0x0200_0x02
            {
                Oil = 125
            });

            jT808Package.Bodies = jT808_0x0200;

            return new JT808Serializer().Serialize(jT808Package);
        }

        /// <summary>
        /// 平台通用应答
        /// </summary>
        /// <returns></returns>
        static byte[] GetJT808ResponseData()
        {
            JT808Package jT808Package = new JT808Package();

            jT808Package.Header = new JT808Header
            {
                MsgId = (ushort)JT808.Protocol.Enums.JT808MsgId.平台通用应答,
                ManualMsgNum = 101,
                TerminalPhoneNo = "123456789012"
            };

            jT808Package.Bodies = new JT808_0x0002();
            return new JT808Serializer().Serialize(jT808Package);
        }


        private static void ServerSokcet_OnReceive(Interface.ISession currentSession, byte[] data)
        {
            IUserToken userToken = (IUserToken)currentSession;
            var jUnpacker = (JUnpacker)userToken.Unpacker;

            jUnpacker.DeCode(data, (b) =>
            {
                var package = new JT808Serializer().Deserialize<JT808Package>(b.AsSpan());
                var body = package.Bodies as JT808_0x0200;

                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"server 收到位置信息汇报,lng:{body.Lng},lat:{body.Lat},time:{body.GPSTime}");
                Console.WriteLine($"server 发送应答,ManualMsgNum:101");

                var jData = GetJT808ResponseData();
                _serverSokcet.SendAsync(userToken.ID, jData);

            });
        }

        private static void ServerSokcet_OnError(string ID, Exception ex)
        {
            Console.WriteLine($"ServerSokcet_OnDisconnected:{ex.Message}");
        }

        private static void ServerSokcet_OnDisconnected(string ID, Exception ex)
        {
            Console.WriteLine($"ServerSokcet_OnDisconnected:{ex.Message}");
        }

        private static void ServerSokcet_OnAccepted(object obj)
        {
            //Console.WriteLine($"ServerSokcet_OnAccepted:{((IUserToken)obj).ID}");
        }


        private static void ClientSokcet_OnError(string ID, Exception ex)
        {
            Console.WriteLine($"ClientSokcet_OnError:{ex.Message}");
        }

        private static void ClientSokcet_OnDisconnected(string ID, Exception ex)
        {
            Console.WriteLine($"ClientSokcet_OnDisconnected:{ex.Message}");
        }

        private static void ClientSokcet_OnReceive(byte[] data)
        {
            _jUnpacker.DeCode(data, (b) =>
            {
                var package = new JT808Serializer().Deserialize<JT808Package>(b.AsSpan());

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"client 收到平台通用应答,MsgNum:{package.Header.MsgNum}");
            });
        }
    }
}
