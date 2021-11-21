using JT808.Protocol;
using JT808.Protocol.MessageBody;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SAEA.Sockets.TcpTest
{
    class Program
    {

        static void Main(string[] args)
        {
            Console.Title = "SAEA.Sockets.TcpTest JT808";

            //jserver
            JServer jServer = new JServer();
            jServer.OnReceive += JServer_OnReceive;
            jServer.Start();

            //jclient
            JClient jClient1 = new JClient();
            jClient1.OnReceive += JClient_OnReceive;
            jClient1.Connect();
            for (int i = 0; i < 10; i++)
            {
                jClient1.SendAsync(GetJT808PositionData());
                Thread.Sleep(500);
            }
            jClient1.Disconnect();

            //jclient
            JClient2 jClient2 = new JClient2();
            jClient2.Connect();
            for (int i = 0; i < 10; i++)
            {
                jClient2.Send(GetJT808PositionData());
                Thread.Sleep(1000);
                var data = jClient2.Receive();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"client 收到平台通用应答,MsgNum:{data.Header.MsgNum}");                              
            }

            jClient2.Disconnect();

            Console.ReadLine();
        }


        private static void JServer_OnReceive(JServer arg1, string arg2, JT808Package arg3)
        {
            var body = arg3.Bodies as JT808_0x0200;
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"server 收到位置信息汇报,lng:{body.Lng},lat:{body.Lat},time:{body.GPSTime}");
            Console.WriteLine($"server 发送应答,ManualMsgNum:101");
            arg1.SendAsync(arg2, GetJT808ResponseData());
        }

        /// <summary>
        /// 位置信息汇报
        /// </summary>
        /// <returns></returns>
        static JT808Package GetJT808PositionData()
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
            jT808_0x0200.CustomLocationAttachData = new Dictionary<byte, JT808_0x0200_CustomBodyBase>();

            jT808Package.Bodies = jT808_0x0200;

            return jT808Package;
        }


        private static void JClient_OnReceive(JClient arg1, JT808Package arg2)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"client 收到平台通用应答,MsgNum:{arg2.Header.MsgNum}");
        }

        /// <summary>
        /// 平台通用应答
        /// </summary>
        /// <returns></returns>
        static JT808Package GetJT808ResponseData()
        {
            JT808Package jT808Package = new JT808Package();

            jT808Package.Header = new JT808Header
            {
                MsgId = (ushort)JT808.Protocol.Enums.JT808MsgId.平台通用应答,
                ManualMsgNum = 101,
                TerminalPhoneNo = "123456789012"
            };

            jT808Package.Bodies = new JT808_0x0002();
            return jT808Package;
        }

    }
}
