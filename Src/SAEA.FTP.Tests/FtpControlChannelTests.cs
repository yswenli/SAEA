/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \  ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.FTP.Tests
*文件名： FtpControlChannelTests
*版本号： net10.0
*唯一标识：17dac1a1-9246-4275-b5e3-7c59bd237acc
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 14:30:00
*描述：控制通道行分帧、应答解析与连接收口的集成用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 14:30:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖服务端问候与多行应答、超长命令收口、客户端多行与非法应答解析
*
*****************************************************************************/
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.FTP;
using SAEA.FTP.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SAEA.FTP.Tests
{
    /// <summary>
    /// 控制通道用例：服务端用裸 TcpClient 校验协议文本，客户端用进程内假服务器校验解析与收口
    /// </summary>
    [TestClass]
    public class FtpControlChannelTests
    {
        string _root;

        FTPServer _server;

        int _port;

        const string User = "tester";

        const string Password = "secret";

        /// <summary>
        /// 为每个用例创建独立的临时根目录并启动本地服务端
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "SAEA.FTP.Tests", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_root);

            StartServer();
        }

        /// <summary>
        /// 释放服务端并递归删除本用例的临时根目录，清理失败不视为用例失败
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            try { _server?.Dispose(); } catch { }

            try { Directory.Delete(_root, true); } catch { }
        }

        void StartServer()
        {
            _port = GetFreePort();

            var config = new ServerConfig()
            {
                IP = "127.0.0.1",
                Port = (ushort)_port,
                BufferSize = 64 * 1024,
                OverwritePolicy = FTPOverwritePolicy.Overwrite,
                DataTimeout = TimeSpan.FromSeconds(30)
            };

            config.Users[User] = new FTPUser(User, Password, _root);

            _server = new FTPServer(config);

            _server.Start();
        }

        /// <summary>
        /// 连上控制端口后首行应是 220 问候，验证活动通知阶段回包可用
        /// </summary>
        [TestMethod]
        public void Server_Greeting_OnConnect()
        {
            using (var client = new TcpClient())
            {
                client.Connect(IPAddress.Loopback, _port);

                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 10000;

                    var greeting = ReadLine(stream);

                    Assert.IsTrue(greeting.StartsWith("220 ", StringComparison.Ordinal), greeting);
                }
            }
        }

        /// <summary>
        /// 登录后 FEAT 应返回 211- 开头、211 结束的多行块，验证原样文本回复
        /// </summary>
        [TestMethod]
        public void Server_FeatReply_MultiLine()
        {
            using (var client = new TcpClient())
            {
                client.Connect(IPAddress.Loopback, _port);

                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 10000;

                    Assert.IsTrue(ReadLine(stream).StartsWith("220 ", StringComparison.Ordinal), "missing greeting");

                    WriteLine(stream, "USER " + User);

                    Assert.IsTrue(ReadLine(stream).StartsWith("331 ", StringComparison.Ordinal), "USER should ask for password");

                    WriteLine(stream, "PASS " + Password);

                    Assert.IsTrue(ReadLine(stream).StartsWith("230 ", StringComparison.Ordinal), "PASS should log in");

                    WriteLine(stream, "FEAT");

                    var first = ReadLine(stream);

                    Assert.IsTrue(first.StartsWith("211-", StringComparison.Ordinal), first);

                    // 读到同一状态码加空格开头的终止行才算收口；超时会把空行当终止行，故用 211 前缀断言。
                    while (true)
                    {
                        var line = ReadLine(stream);

                        Assert.IsTrue(line.Length > 0, "FEAT block ended without a terminator");

                        if (line.StartsWith("211 ", StringComparison.Ordinal)) break;
                    }
                }
            }
        }

        /// <summary>
        /// 未登录发未知命令应回 502 且连接仍可继续登录
        /// </summary>
        [TestMethod]
        public void Server_UnknownCommand_RepliesNotImplemented()
        {
            using (var client = new TcpClient())
            {
                client.Connect(IPAddress.Loopback, _port);

                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 10000;

                    Assert.IsTrue(ReadLine(stream).StartsWith("220 ", StringComparison.Ordinal), "missing greeting");

                    WriteLine(stream, "FOO");

                    var unknown = ReadLine(stream);

                    Assert.IsTrue(unknown.StartsWith("502 ", StringComparison.Ordinal), unknown);

                    WriteLine(stream, "USER " + User);

                    var user = ReadLine(stream);

                    Assert.IsTrue(user.StartsWith("331 ", StringComparison.Ordinal), user);
                }
            }
        }

        /// <summary>
        /// 单行超过 8KB 上限且没有换行时，服务端应主动收口而不是无限累积
        /// </summary>
        [TestMethod]
        public void Server_OverlongCommandLine_ClosesConnection()
        {
            using (var client = new TcpClient())
            {
                client.Connect(IPAddress.Loopback, _port);

                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 15000;

                    Assert.IsTrue(ReadLine(stream).StartsWith("220 ", StringComparison.Ordinal), "missing greeting");

                    var payload = new byte[9 * 1024];

                    for (var i = 0; i < payload.Length; i++) payload[i] = (byte)'A';

                    stream.Write(payload, 0, payload.Length);

                    var closed = false;

                    try
                    {
                        closed = stream.ReadByte() < 0;
                    }
                    catch (IOException ex)
                    {
                        // 对端复位同样算收口；只有等待超时才是用例失败。
                        var socketError = ex.InnerException as SocketException;

                        if (socketError != null && socketError.SocketErrorCode == SocketError.TimedOut)
                        {
                            throw;
                        }

                        closed = true;
                    }

                    Assert.IsTrue(closed, "expected the server to close the connection");
                }
            }
        }

        /// <summary>
        /// 假服务器发多行 220 问候后应能完成登录并继续执行命令
        /// </summary>
        [TestMethod]
        public void Client_MultiLineGreeting_Connects()
        {
            using (var server = new FakeFtpServer("220-Welcome\r\n220 Ready\r\n", FakeLoginReply))
            using (var client = CreateClient(server.Port))
            {
                client.Connect();

                Assert.IsTrue(client.Connected, "client should stay connected");

                Assert.AreEqual("/", client.CurrentDir());
            }
        }

        /// <summary>
        /// 假服务器发非法应答行时应抛 IOException，而不是把非法文本当成应答
        /// </summary>
        [TestMethod]
        public void Client_MalformedReply_ThrowsIOException()
        {
            using (var server = new FakeFtpServer("abc\r\n", FakeLoginReply))
            using (var client = CreateClient(server.Port))
            {
                Assert.ThrowsException<IOException>(() => client.Connect());
            }
        }

        /// <summary>
        /// 假服务器发完问候即关闭时，下一命令应立即抛 IOException 而不是等到应答超时
        /// </summary>
        [TestMethod]
        public void Client_ServerClosedWhileWaiting_ThrowsIOException()
        {
            using (var server = new FakeFtpServer("220 Ready\r\n", FakeLoginReply, true))
            using (var client = CreateClient(server.Port))
            {
                var watch = Stopwatch.StartNew();

                Assert.ThrowsException<IOException>(() => client.Connect());

                watch.Stop();

                Assert.IsTrue(watch.Elapsed < TimeSpan.FromSeconds(5), "closed connection should fail before the command timeout, elapsed: " + watch.Elapsed);
            }
        }

        FTPClient CreateClient(int port)
        {
            return new FTPClient(new ClientConfig()
            {
                IP = "127.0.0.1",
                Port = port,
                UserName = User,
                Password = Password,
                BufferSize = 32 * 1024,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                CommandTimeout = TimeSpan.FromSeconds(10),
                DataTimeout = TimeSpan.FromSeconds(30)
            });
        }

        static string FakeLoginReply(string command)
        {
            if (command.StartsWith("USER", StringComparison.OrdinalIgnoreCase))
                return "331 Password required.\r\n";

            if (command.StartsWith("PASS", StringComparison.OrdinalIgnoreCase))
                return "230 Logged in.\r\n";

            if (command.StartsWith("FEAT", StringComparison.OrdinalIgnoreCase))
                return "211-Features:\r\n REST STREAM\r\n211 End\r\n";

            if (command.StartsWith("PWD", StringComparison.OrdinalIgnoreCase))
                return "257 \"/\" is current directory.\r\n";

            return "200 OK.\r\n";
        }

        static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        static void WriteLine(NetworkStream stream, string line)
        {
            var data = Encoding.UTF8.GetBytes(line + "\r\n");
            stream.Write(data, 0, data.Length);
        }

        static string ReadLine(NetworkStream stream)
        {
            var bytes = new List<byte>();

            while (true)
            {
                var value = stream.ReadByte();

                if (value < 0) break;

                if (value == '\n') break;

                if (value != '\r') bytes.Add((byte)value);
            }

            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        /// <summary>
        /// 进程内假 FTP 服务器：按脚本回问候与命令应答，回完即关连接用于验证收口
        /// </summary>
        sealed class FakeFtpServer : IDisposable
        {
            readonly TcpListener _listener;

            readonly Thread _thread;

            readonly string _greeting;

            readonly Func<string, string> _reply;

            readonly bool _closeAfterGreeting;

            volatile bool _disposed;

            public int Port { get; private set; }

            public FakeFtpServer(string greeting, Func<string, string> reply, bool closeAfterGreeting = false)
            {
                _greeting = greeting;
                _reply = reply;
                _closeAfterGreeting = closeAfterGreeting;

                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

                _thread = new Thread(Serve);
                _thread.IsBackground = true;
                _thread.Start();
            }

            void Serve()
            {
                try
                {
                    using (var client = _listener.AcceptTcpClient())
                    using (var stream = client.GetStream())
                    {
                        stream.ReadTimeout = 10000;

                        var greeting = Encoding.UTF8.GetBytes(_greeting);
                        stream.Write(greeting, 0, greeting.Length);

                        if (_closeAfterGreeting) return;

                        while (!_disposed)
                        {
                            var line = ReadLine(stream);

                            if (string.IsNullOrEmpty(line)) return;

                            var response = _reply(line);
                            if (response == null) return;

                            var data = Encoding.UTF8.GetBytes(response);
                            stream.Write(data, 0, data.Length);
                        }
                    }
                }
                catch (SocketException)
                {
                }
                catch (IOException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }

            public void Dispose()
            {
                _disposed = true;

                try { _listener.Stop(); } catch { }
                try { _thread.Join(2000); } catch { }
            }
        }
    }
}