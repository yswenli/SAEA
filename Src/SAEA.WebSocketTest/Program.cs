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
*电子邮箱：yswenli@outlook.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 14:18:00
*修改人： yswenli
*版本号： net10.0
*描述：改用处理器链装配，转发压测加发送窗口与末批等待，删除已移除的事件订阅
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Common.IO;
using SAEA.Sockets.Pipeline;
using SAEA.WebSocket;
using SAEA.WebSocket.Model;
using SAEA.WebSocket.Type;

using System;
using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
            var result = coder.Decode(new ReadOnlySequence<byte>(frame));
            
            // 解码后，result中的WSProtocal.Content是从池租用的（因为>4KB）
            // 需要正确Dispose这些对象
            foreach (var item in result.Frames)
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
            result = coder.Decode(new ReadOnlySequence<byte>(combinedFrame));
            
            // 小数据(<4KB)不是池化的，不需要特殊处理
        }

        /// <summary>
        /// 构造用于测试的 WebSocket 帧
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

        #endregion

        #region test2 ssl

        static void Init2()
        {
            ConsoleHelper.WriteLine("WSSServer 正在初始化....", ConsoleColor.Green);

            var pfxPath = PathHelper.GetFullName("yswenli.pfx");
            _server = new WSServer(39656, System.Security.Authentication.SslProtocols.Tls12, pfxPath, "yswenli");

            // 收包与回包都改在处理器链上完成：帧已被解码成 WSProtocal，链尾的异常处理器负责收口。
            _server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new ServerEchoHandler(_server, id));

            _server.Start();

            ConsoleHelper.WriteLine("WSSServer 就绪,回车启动客户端", ConsoleColor.Green);
        }

        #endregion

        #region test3 workman

        static void Init3()
        {
            ConsoleHelper.WriteLine("WSClient 正在连接到WorkMan服务器...", ConsoleColor.DarkGray);

            var url = "ws://120.79.233.58:7272";

            WSClient client = new WSClient(url, SubProtocolType.Json);
            client.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new ClientPrintHandler("WSClient"));

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
            client.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new ClientPrintHandler("WSClient"));

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
            _server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new ServerEchoHandler(_server, id));
            _server.Start();
            ConsoleHelper.WriteLine("WSServer 就绪,回车启动客户端", ConsoleColor.Green);

            ConsoleHelper.ReadLine();

            WSClient client = new WSClient();
            client.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new ClientPrintHandler("WSClient", true));

            ConsoleHelper.WriteLine("WSClient 正在连接到服务器...", ConsoleColor.DarkGray);
            var connected = client.Connect();

            Console.WriteLine("发送消息到ws服务器，转发测试开始");
            _sw = Stopwatch.StartNew();
            for (int i = 0; i < _maxSize; i++)
            {
                client.Send($"测试:hello world {i} {DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}");

                // 出站待写项默认上限 256，超出即拒绝；压测本身也要控制未回包的消息数，否则丢消息会让统计失真。
                if ((i & (SendWindow - 1)) == SendWindow - 1)
                    WaitForEcho(i + 1 - SendWindow, 5000);
            }

            // 最后一批回包可能仍在途：等它们收完，统计的总耗时与条数才覆盖整轮往返。
            WaitForEcho(_maxSize, 10000);
        }

        /// <summary>
        /// 发送窗口：让未回包的消息数保持在出站预算的一半以内。
        /// </summary>
        private const int SendWindow = 128;

        /// <summary>
        /// 等待回包计数追上给定值；超时即放弃等待，让压测按实际收到的条数收敛。
        /// </summary>
        /// <param name="target">期望已回包条数。</param>
        /// <param name="timeoutMilliseconds">最长等待毫秒数。</param>
        private static void WaitForEcho(int target, int timeoutMilliseconds)
        {
            var sw = Stopwatch.StartNew();
            while (Volatile.Read(ref _count) < target)
            {
                if (sw.ElapsedMilliseconds > timeoutMilliseconds)
                {
                    Console.WriteLine($"等待回包超时，已发送 {target} 条、已回包 {_count} 条");
                    return;
                }

                Thread.Sleep(1);
            }
        }

        /// <summary>
        /// 客户端打印处理器：逐条打印收到的帧，并在收口时打印断开原因、转发异常给链尾收口。
        /// </summary>
        private sealed class ClientPrintHandler : InboundHandlerAdapter
        {
            private readonly string _name;

            private readonly bool _perf;

            /// <summary>
            /// 绑定客户端名称，用于日志前缀。
            /// </summary>
            /// <param name="name">日志前缀。</param>
            /// <param name="perf">是否为转发性能模式：按条计数并统计总耗时。</param>
            public ClientPrintHandler(string name, bool perf = false)
            {
                _name = name;
                _perf = perf;
            }

            /// <summary>
            /// 打印收到的帧；转发性能用例按条计数并统计总耗时。
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文。</param>
            /// <param name="msg">已解码的协议帧。</param>
            /// <returns>处理完成后结束的任务。</returns>
            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var protocal = msg as WSProtocal;
                if (protocal == null)
                    return ctx.FireChannelReadAsync(msg);

                // 帧体由池化缓冲承载，只能在本次回调内读取。
                var text = Encoding.UTF8.GetString(protocal.Content.Span);

                if (!_perf)
                {
                    ConsoleHelper.WriteLine("WSClient 收到的消息：{0}", ConsoleColor.DarkGray, text);
                    return Task.CompletedTask;
                }

                Interlocked.Increment(ref _count);

                if (_count == _maxSize)
                {
                    _sw.Stop();
                    Console.WriteLine($"转发测试已完成，用时：{_sw.Elapsed.TotalSeconds}秒，回车结束测试");
                }
                else if (_count % 1000 == 0)
                {
                    Console.Write($"\r客户端已收到服务器转发消息条数：{_count}");
                }

                return Task.CompletedTask;
            }

            /// <summary>
            /// 打印连接断开。
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文。</param>
            /// <returns>处理完成后结束的任务。</returns>
            public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
            {
                ConsoleHelper.WriteLine(_name + " 连接已断开", ConsoleColor.DarkGray);
                return Task.CompletedTask;
            }

            /// <summary>
            /// 打印异常并继续向下游传播，由链尾决定是否断链。
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文。</param>
            /// <param name="ex">待处理的异常。</param>
            /// <returns>下游处理完成后结束的任务。</returns>
            public override Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
            {
                ConsoleHelper.WriteLine(_name + " 出现异常：{0}", ConsoleColor.DarkGray, ex.Message);
                return ctx.FireExceptionCaughtAsync(ex);
            }
        }

        /// <summary>
        /// 服务端回显处理器：打印收到的帧并原样回给同一会话。
        /// </summary>
        private sealed class ServerEchoHandler : InboundHandlerAdapter
        {
            private readonly WSServer _server;

            private readonly string _id;

            /// <summary>
            /// 绑定所属服务端与会话标识。
            /// </summary>
            /// <param name="server">所属服务端。</param>
            /// <param name="id">会话标识。</param>
            public ServerEchoHandler(WSServer server, string id)
            {
                _server = server;
                _id = id;
            }

            /// <summary>
            /// 打印并回写收到的帧；帧体先复制，避免回写读取已被归还的池化缓冲。
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文。</param>
            /// <param name="msg">已解码的协议帧。</param>
            /// <returns>处理完成后结束的任务。</returns>
            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var protocal = msg as WSProtocal;
                if (protocal == null)
                    return ctx.FireChannelReadAsync(msg);

                ConsoleHelper.WriteLine("WSServer 收到{0}的消息：{1}", ConsoleColor.Green, _id,
                    Encoding.UTF8.GetString(protocal.Content.Span));

                _server.Reply(_id, new WSProtocal(protocal.Type, protocal.Content.ToArray()));

                return Task.CompletedTask;
            }

            /// <summary>
            /// 打印会话断开。
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文。</param>
            /// <returns>处理完成后结束的任务。</returns>
            public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
            {
                ConsoleHelper.WriteLine("WSServer 收到{0}已断开！", ConsoleColor.Green, _id);
                return Task.CompletedTask;
            }
        }

        #endregion


    }
}