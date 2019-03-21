/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：ASEAWebSocketTest
*文件名： Class1
*版本号： v4.3.1.2
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
*版本号： v4.3.1.2
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.WebSocket;
using SAEA.WebSocket.Model;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.WebSocketTest
{
    class Program
    {
        static WSServer _server = new WSServer();

        static void Main(string[] args)
        {
            ConsoleHelper.WriteLine("WSServer 正在初始化....", ConsoleColor.Green);
            _server.OnMessage += Server_OnMessage;
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


                var loop = true;

                Task.Run(() =>
                {
                    while (loop)
                    {
                        ConsoleHelper.WriteLine("WSClient 正在发送消息...", ConsoleColor.DarkGray);

                        client.Send("hello world!");

                        Thread.Sleep(1000);
                    }
                });

                ConsoleHelper.ReadLine();
                loop = false;
                ConsoleHelper.WriteLine("WSClient 正在ping服务器...", ConsoleColor.DarkGray);
                Thread.Sleep(2000);
                client.Ping();


                ConsoleHelper.ReadLine();
                ConsoleHelper.WriteLine("WSClient 正在断开连接...");
                Thread.Sleep(1000);
                client.Close();
            }
            else
            {
                ConsoleHelper.WriteLine("WSClient 连接失败", ConsoleColor.DarkGray);
            }


            ConsoleHelper.ReadLine();
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

        private static void Client_OnPong(DateTime date)
        {
            ConsoleHelper.WriteLine("WSClient 收到pong：{0}", ConsoleColor.DarkGray, date);
        }

        private static void Client_OnDisconnected(string ID, Exception ex)
        {
            ConsoleHelper.WriteLine("WSClient 连接已断开：" + ex.Message, ConsoleColor.DarkGray);
        }


    }
}
