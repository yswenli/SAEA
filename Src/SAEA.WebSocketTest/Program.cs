/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：ASEAWebSocketTest
*文件名： Class1
*版本号： v7.0.0.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

using SAEA.Common;
using SAEA.Common.IO;
using SAEA.Http;
using SAEA.WebSocket;
using SAEA.WebSocket.Model;

namespace SAEA.WebSocketTest
{
    class Program
    {
        static WSServer _server;

        static void Main(string[] args)
        {
            //Init1();

            //Init2();

            //Init3();

            //Init4();

            Init5();

            ConsoleHelper.ReadLine();
        }


        #region test1

        static void Init1()
        {
            WebHost webHost = new WebHost(port: 18080, root: "Html");

            webHost.Start();


            ConsoleHelper.WriteLine("WSServer 正在初始化....", ConsoleColor.Green);
            _server = new WSServer();
            _server.OnMessage += Server_OnMessage;
            _server.OnDisconnected += _server_OnDisconnected;
            _server.Start();
            ConsoleHelper.WriteLine("WSServer 就绪,回车启动客户端", ConsoleColor.Green);

            ConsoleHelper.ReadLine();

            WSClient client = new WSClient();
            client.OnPong += Client_OnPong;
            client.OnMessage += Client_OnMessage;
            client.OnError += Client_OnError;
            client.OnDisconnected += Client_OnDisconnected;

            ConsoleHelper.WriteLine("WSClient 正在连接到服务器...", ConsoleColor.DarkGray);

            var connected = client.Connect();

            if (connected)
            {
                ConsoleHelper.WriteLine("WSClient 连接成功，回车测试消息", ConsoleColor.DarkGray);
                ConsoleHelper.ReadLine();
                //client.Close();
                //ConsoleHelper.ReadLine();


                ConsoleHelper.WriteLine("WSClient 正在ping服务器...", ConsoleColor.DarkGray);
                client.Ping();


                ConsoleHelper.WriteLine("WSClient 正在发送消息...", ConsoleColor.DarkGray);

                client.Send($"hello world!{DateTimeHelper.Now.ToString("HH:mm:ss.fff")}");
                ConsoleHelper.ReadLine();


                ConsoleHelper.ReadLine();
                ConsoleHelper.WriteLine("WSClient 正在断开连接...");
                client.Close();

                ConsoleHelper.WriteLine("测试已完成");
                ConsoleHelper.ReadLine();
            }
            else
            {
                ConsoleHelper.WriteLine("WSClient 连接失败", ConsoleColor.DarkGray);
            }
        }


        private static void Server_OnMessage(string id, WSProtocal data)
        {
            ConsoleHelper.WriteLine("WSServer 收到{0}的消息：{1}", ConsoleColor.Green, id, Encoding.UTF8.GetString(data.Content));

            _server.Reply(id, data);
        }


        private static void Client_OnError(string ID, Exception ex)
        {
            ConsoleHelper.WriteLine("WSClient 出现异常：{0}", ConsoleColor.DarkGray, ex.Message);
        }

        private static void Client_OnMessage(WSProtocal data)
        {
            ConsoleHelper.WriteLine("WSClient 收到的消息：{0}", ConsoleColor.DarkGray, Encoding.UTF8.GetString(data.Content));
        }

        private static void Client_OnPong(string date)
        {
            ConsoleHelper.WriteLine("WSClient 收到pong：{0}", ConsoleColor.DarkGray, date);
        }

        private static void Client_OnDisconnected(string ID, Exception ex)
        {
            ConsoleHelper.WriteLine("WSClient Client_OnDisconnected 连接已断开：" + ex.Message, ConsoleColor.DarkGray);
        }

        #endregion

        #region test2 ssl

        static void Init2()
        {
            ConsoleHelper.WriteLine("WSSServer 正在初始化....", ConsoleColor.Green);

            var pfxPath = PathHelper.GetFullName("yswenli.pfx");
            _server = new WSServer(39656, System.Security.Authentication.SslProtocols.Tls12, pfxPath, "yswenli");
            _server.OnMessage += Server_OnMessage;
            _server.OnDisconnected += _server_OnDisconnected;
            _server.Start();

            ConsoleHelper.WriteLine("WSSServer 就绪,回车启动客户端", ConsoleColor.Green);
        }

        private static void _server_OnDisconnected(string id)
        {
            ConsoleHelper.WriteLine("WSServer 收到{0}已断开！", ConsoleColor.Green, id);
        }

        private static void Server_OnMessage2(string id, WSProtocal data)
        {
            _server.Reply(id, data);
        }


        #endregion

        #region test3 workman

        static void Init3()
        {
            ConsoleHelper.WriteLine("WSClient 正在连接到WorkMan服务器...", ConsoleColor.DarkGray);

            var url = "ws://120.79.233.58:7272";

            WSClient client = new WSClient(url, SubProtocolType.Json);
            client.OnPong += Client_OnPong;
            client.OnMessage += Client_OnMessage;
            client.OnError += Client_OnError;
            client.OnDisconnected += Client_OnDisconnected;

            var connected = client.Connect();

            if (connected)
            {
                ConsoleHelper.WriteLine("WSClient 连接成功，回车测试消息", ConsoleColor.DarkGray);
                ConsoleHelper.ReadLine();

                client.Ping();

                ConsoleHelper.WriteLine("WSClient 正在发送消息...", ConsoleColor.DarkGray);

                client.Send($"hello world!{DateTimeHelper.Now.ToString("HH:mm:ss.fff")}");

                ConsoleHelper.WriteLine("WSClient 已发送消息", ConsoleColor.DarkGray);

                ConsoleHelper.ReadLine();



                ConsoleHelper.WriteLine("回车WSClient 断开连接");

                ConsoleHelper.ReadLine();

                client.Close();

                ConsoleHelper.ReadLine();
            }
            else
            {
                ConsoleHelper.WriteLine("WSClient 连接失败", ConsoleColor.DarkGray);
            }
        }

        #endregion

        #region test4 other
        static void Init4()
        {
            ConsoleHelper.WriteLine("WSClient 正在连接到WorkMan服务器...", ConsoleColor.DarkGray);

            var url = "ws://123.207.136.134:9010/ajaxchattest";

            WSClient client = new WSClient(url, SubProtocolType.Empty, "http://coolaf.com");
            client.OnPong += Client_OnPong;
            client.OnMessage += Client_OnMessage;
            client.OnError += Client_OnError;
            client.OnDisconnected += Client_OnDisconnected;

            var connected = client.Connect();

            if (connected)
            {
                ConsoleHelper.WriteLine("WSClient 连接成功，回车测试消息", ConsoleColor.DarkGray);
                ConsoleHelper.ReadLine();

                client.Ping();

                ConsoleHelper.WriteLine("WSClient 正在发送消息...", ConsoleColor.DarkGray);

                client.Send($"1111");

                client.Send($"1111");

                client.Send($"1111");

                ConsoleHelper.WriteLine("WSClient 已发送消息", ConsoleColor.DarkGray);



                ConsoleHelper.WriteLine("回车WSClient 断开连接");

                ConsoleHelper.ReadLine();

                client.Close();
            }
            else
            {
                ConsoleHelper.WriteLine("WSClient 连接失败", ConsoleColor.DarkGray);
            }
        }

        static Stopwatch _sw;
        static int _maxSize = 10000;
        static int _count = 0;

        static void Init5()
        {
            ConsoleHelper.WriteLine("WSServer 正在初始化....", ConsoleColor.Green);
            _server = new WSServer(count: 1);
            _server.OnMessage += Server_OnMessage2;
            _server.OnDisconnected += _server_OnDisconnected;
            _server.Start();
            ConsoleHelper.WriteLine("WSServer 就绪,回车启动客户端", ConsoleColor.Green);

            ConsoleHelper.ReadLine();

            WSClient client = new WSClient();
            client.OnPong += Client_OnPong;
            client.OnMessage += Client_OnMessage2;
            client.OnError += Client_OnError;
            client.OnDisconnected += Client_OnDisconnected;

            ConsoleHelper.WriteLine("WSClient 正在连接到服务器...", ConsoleColor.DarkGray);
            var connected = client.Connect();

            Console.WriteLine("发送消息到ws服务器，转发测试开始");
            _sw = Stopwatch.StartNew();
            for (int i = 0; i < _maxSize; i++)
            {
                client.Send($"测试:hello world {i} {DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}");
            }
        }
        private static void Client_OnMessage2(WSProtocal protocal)
        {
            Interlocked.Increment(ref _count);
            var str = System.Text.Encoding.UTF8.GetString(protocal.Content);
            Console.Write($"\r客户端已收到服务器转发消息条数：{_count}");
            if (_count == _maxSize)
            {
                _sw.Stop();
                Console.WriteLine($"转发测试已完成，用时：{_sw.Elapsed.TotalSeconds}秒");
            }
        }
        #endregion


    }
}
