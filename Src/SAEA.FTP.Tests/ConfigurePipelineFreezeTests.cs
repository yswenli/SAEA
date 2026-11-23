/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.FTP.Tests
*文件名： ConfigurePipelineFreezeTests
*版本号： net10.0
*唯一标识：edc10cb6-f91b-4c5d-9251-2b388055ca16
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 14:30:00
*描述：FTP 服务端与客户端装配回调冻结的校验用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 14:30:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，校验 Start/Connect 之后再赋值装配回调会抛异常
*
*****************************************************************************/
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.FTP;
using SAEA.FTP.Model;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace SAEA.FTP.Tests
{
    /// <summary>
    /// 处理器链装配回调的冻结语义：服务端 Start 之前、客户端首次 Connect 之前可赋值，之后赋值必须抛异常
    /// </summary>
    [TestClass]
    public class ConfigurePipelineFreezeTests
    {
        string _root;

        FTPServer _server;

        int _port;

        const string User = "tester";

        const string Password = "secret";

        /// <summary>
        /// 为每个用例创建独立的临时根目录与一台尚未启动的服务端
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
        /// 服务端启动前可赋值装配回调，启动后再赋值应抛 InvalidOperationException
        /// </summary>
        [TestMethod]
        public void FTPServer_ConfigurePipelineAfterStart_Throws()
        {
            _server.ConfigurePipeline = (id, pipeline) => { };

            _server.Start();

            Assert.ThrowsExactly<InvalidOperationException>(
                () => _server.ConfigurePipeline = (id, pipeline) => { });
        }

        /// <summary>
        /// 客户端首次连接前可赋值装配回调，连上后再赋值应抛 InvalidOperationException
        /// </summary>
        [TestMethod]
        public void FTPClient_ConfigurePipelineAfterConnect_Throws()
        {
            _server.Start();

            using (var client = new FTPClient(new ClientConfig()
            {
                IP = "127.0.0.1",
                Port = _port,
                UserName = User,
                Password = Password,
                BufferSize = 32 * 1024,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                CommandTimeout = TimeSpan.FromSeconds(10),
                DataTimeout = TimeSpan.FromSeconds(30)
            }))
            {
                client.ConfigurePipeline = (id, pipeline) => { };

                client.Connect();

                Assert.ThrowsExactly<InvalidOperationException>(
                    () => client.ConfigurePipeline = (id, pipeline) => { });
            }
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
    }
}