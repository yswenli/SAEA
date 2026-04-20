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
using SAEA.Common.Caching;
using SAEA.Common.IO;
using SAEA.Http;
using SAEA.WebSocket;
using SAEA.WebSocket.Model;
using SAEA.WebSocket.Type;

namespace SAEA.WebSocketTest
{
    class Program
    {
        static WSServer _server;

        static void Main(string[] args)
        {
            Init1();

            //Init2();

            //Init3();

            //Init4();

            Init5();

            ConsoleHelper.ReadLine();
        }


        #region test1

        static void Init1()
        {

            // Arrange
            var coder = new WSCoder();
            var payloadData = new byte[5000]; // > 4KB
            new Random().NextBytes(payloadData);

            // Create a WebSocket frame
            var frame = CreateWebSocketFrame(0x01, payloadData, false); // Text frame, no mask

            // Act
            var result = coder.Decode(frame);
            
            // 解码后，result中的WSProtocal.Content是从池租用的（因为>4KB）
            // 需要正确Dispose这些对象
            foreach (var item in result)
            {
                if (item is WSProtocal wsProtocal)
                {
                    wsProtocal.Dispose();
                }
            }


            // 测试池化缓冲区的正确用法：从池租用
            var largeData = MemoryPoolManager.Rent(5000);  // 从池租用
            new Random().NextBytes(largeData);
            var protocal = new WSProtocal(WSProtocalType.Binary, largeData)
            {
                IsPooled = true  // 正确：数组确实是从池租用的
            };

            protocal.Dispose();  // 正确归还到池


            var payloadData1 = Encoding.UTF8.GetBytes("Frame 1");
            var payloadData2 = Encoding.UTF8.GetBytes("Frame 2");

            var frame1 = CreateWebSocketFrame(0x01, payloadData1, false);
            var frame2 = CreateWebSocketFrame(0x01, payloadData2, false);

            var combinedFrame = new byte[frame1.Length + frame2.Length];
            Buffer.BlockCopy(frame1, 0, combinedFrame, 0, frame1.Length);
            Buffer.BlockCopy(frame2, 0, combinedFrame, frame1.Length, frame2.Length);

            // Act
            result = coder.Decode(combinedFrame);
            
            // 小数据(<4KB)不是池化的，不需要特殊处理
        }

        /// <summary>
        /// Creates a WebSocket frame for testing
        /// </summary>
        static byte[] CreateWebSocketFrame(byte opcode, byte[] payload, bool masked)
        {
            var frame = new System.Collections.Generic.List<byte>();

            // First byte: FIN=1, RSV=0, opcode
            frame.Add((byte)(0x80 | opcode));

            // Second byte: MASK, payload length
            byte secondByte = 0;
            if (masked) secondByte |= 0x80;

            if (payload.Length < 126)
            {
                secondByte |= (byte)payload.Length;
                frame.Add(secondByte);
            }
            else if (payload.Length < 65536)
            {
                secondByte |= 126;
                frame.Add(secondByte);
                frame.Add((byte)((payload.Length >> 8) & 0xFF));
                frame.Add((byte)(payload.Length & 0xFF));
            }
            else
            {
                secondByte |= 127;
                frame.Add(secondByte);
                for (int i = 7; i >= 0; i--)
                {
                    frame.Add((byte)((payload.Length >> (i * 8)) & 0xFF));
                }
            }

            // Masking key (if masked)
            if (masked)
            {
                var maskKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
                frame.AddRange(maskKey);

                // Mask payload
                var maskedPayload = new byte[payload.Length];
                for (int i = 0; i < payload.Length; i++)
                {
                    maskedPayload[i] = (byte)(payload[i] ^ maskKey[i % 4]);
                }
                frame.AddRange(maskedPayload);
            }
            else
            {
                frame.AddRange(payload);
            }

            return frame.ToArray();
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
        #endregion


        #region test5 转发性能测试


        static Stopwatch _sw;
        static int _maxSize = 100000;
        static int _count = 0;

        static void Init5()
        {
            ConsoleHelper.WriteLine("WSServer 正在初始化....", ConsoleColor.Green);
            _server = new WSServer(maxConnects: 1);
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
                Console.WriteLine($"转发测试已完成，用时：{_sw.Elapsed.TotalSeconds}秒，回车结束测试");
            }
        }

        #endregion


    }
}
