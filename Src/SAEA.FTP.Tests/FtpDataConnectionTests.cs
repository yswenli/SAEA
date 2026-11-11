/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.FTP.Tests
*文件名： FtpDataConnectionTests
*版本号： net10.0
*唯一标识：985551a3-1fc6-4d84-9d07-806ccd52b905
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 17:52:00
*描述：客户端数据连接门面的读写、半关闭与重复收口用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 17:52:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖下载读结束、上传半关闭收 226 与重复 Close 不影响控制连接
*
*****************************************************************************/
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.FTP;
using SAEA.FTP.Model;
using SAEA.FTP.Net;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace SAEA.FTP.Tests
{
    /// <summary>
    /// 客户端数据连接门面集成测试：直连本仓库服务端，覆盖读写、半关闭与重复收口
    /// </summary>
    [TestClass]
    public class FtpDataConnectionTests
    {
        string _root;

        FTPServer _server;

        int _port;

        const string User = "tester";

        const string Password = "secret";

        /// <summary>
        /// 为每个用例创建独立临时根目录并启动服务端，控制端口由系统分配
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "SAEA.FTP.Tests", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_root);

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
        /// 释放服务端并递归删除本用例的临时根目录，清理失败不视为用例失败
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            try { _server?.Dispose(); } catch { }

            try { Directory.Delete(_root, true); } catch { }
        }

        /// <summary>
        /// 下载时从数据流读到 0 为止，字节与服务端文件完全一致且随后收到 226
        /// </summary>
        [TestMethod]
        public void OpenPassive_下载_读到全部字节并读到结束()
        {
            var payload = CreatePayload(8192, 11);

            File.WriteAllBytes(Path.Combine(_root, "remote.bin"), payload);

            using (var ctl = CreateControl())
            {
                ctl.Connect();

                using (var data = ctl.OpenPassive(ctl.DataTimeout))
                {
                    var reply = ctl.Send("RETR remote.bin");

                    Assert.AreEqual(150, reply.Code);

                    var buffer = new byte[4096];

                    var received = new MemoryStream();

                    // 数据通道无消息边界：读到 0 才代表对端已 FIN 且尾块已交付。
                    while (true)
                    {
                        var count = data.Stream.Read(buffer, 0, buffer.Length);

                        if (count <= 0) break;

                        received.Write(buffer, 0, count);
                    }

                    var complete = ctl.ReadReply();

                    Assert.AreEqual(226, complete.Code);
                    CollectionAssert.AreEqual(payload, received.ToArray());
                }
            }
        }

        /// <summary>
        /// 上传写完并半关闭发送方向后收到 226，服务端落盘字节与原数据完全一致
        /// </summary>
        [TestMethod]
        public void OpenPassive_上传_半关闭后收到226且远端完整()
        {
            var payload = CreatePayload(4096, 12);

            using (var ctl = CreateControl())
            {
                ctl.Connect();

                using (var data = ctl.OpenPassive(ctl.DataTimeout))
                {
                    var reply = ctl.Send("STOR uploaded.bin");

                    Assert.AreEqual(150, reply.Code);

                    data.Stream.Write(payload, 0, payload.Length);

                    data.Stream.Flush();

                    // 服务端靠 FIN 判定数据结束，半关闭必须发生在全部写完成交接之后。
                    data.ShutdownSend();

                    var complete = ctl.ReadReply();

                    Assert.AreEqual(226, complete.Code);
                }
            }

            CollectionAssert.AreEqual(payload, File.ReadAllBytes(Path.Combine(_root, "uploaded.bin")));
        }

        /// <summary>
        /// 数据连接重复 Close 与 Dispose 均不抛异常，且不影响控制连接继续执行命令
        /// </summary>
        [TestMethod]
        public void Close_重复调用_不抛异常且控制连接仍可用()
        {
            using (var ctl = CreateControl())
            {
                ctl.Connect();

                var data = ctl.OpenPassive(ctl.DataTimeout);

                data.Close();
                data.Close();
                data.Dispose();

                var reply = ctl.Send("PWD");

                Assert.AreEqual(257, reply.Code);
            }
        }

        /// <summary>
        /// 创建并登录一条控制连接，数据超时用于限制数据连接的读写等待
        /// </summary>
        /// <returns>已完成握手的控制连接</returns>
        FtpControlConnection CreateControl()
        {
            return new FtpControlConnection(new ClientConfig()
            {
                IP = "127.0.0.1",
                Port = _port,
                UserName = User,
                Password = Password,
                BufferSize = 32 * 1024,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                CommandTimeout = TimeSpan.FromSeconds(20),
                DataTimeout = TimeSpan.FromSeconds(30)
            }, null);
        }

        /// <summary>
        /// 取一个当前空闲的本地端口，避免固定端口在并发环境下互相占用
        /// </summary>
        /// <returns>系统分配的可用端口</returns>
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

        /// <summary>
        /// 生成确定性的测试字节，便于逐字节比对
        /// </summary>
        /// <param name="size">字节数</param>
        /// <param name="seed">随机种子</param>
        /// <returns>测试数据</returns>
        static byte[] CreatePayload(int size, int seed)
        {
            var data = new byte[size];

            var random = new Random(seed);

            random.NextBytes(data);

            return data;
        }
    }
}