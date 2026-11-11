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
*命名空间：SAEA.WebSocket.Tests
*文件名： ConfigurePipelineFreezeTests
*版本号： net10.0
*唯一标识：5e85c0d0-bcd9-45ff-b9d8-e02cf9b2451a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 10:58:00
*描述：WebSocket 服务端与客户端装配回调冻结的校验用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 10:58:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，校验 Start/Connect 之后再赋值装配回调会抛异常
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.WebSocket.Tests
{
    /// <summary>
    /// 处理器链装配回调的冻结语义：服务端 Start 之前、客户端首次 Connect 之前可赋值，之后赋值必须抛异常。
    /// </summary>
    [TestClass]
    public class ConfigurePipelineFreezeTests
    {
        /// <summary>
        /// 服务端启动后再赋值装配回调应抛 InvalidOperationException。
        /// </summary>
        [TestMethod]
        public void WSServer_ConfigurePipelineAfterStart_Throws()
        {
            var port = GetFreePort();

            var server = new WSServer(port);

            server.Start();

            try
            {
                Assert.ThrowsException<InvalidOperationException>(
                    () => server.ConfigurePipeline = (id, pipeline) => { });
            }
            finally
            {
                server.Stop();
            }
        }

        /// <summary>
        /// 客户端握手成功后再赋值装配回调应抛 InvalidOperationException。
        /// </summary>
        [TestMethod]
        public void WSClient_ConfigurePipelineAfterConnect_Throws()
        {
            var port = GetFreePort();

            var server = new WSServer(port);

            server.Start();

            var client = new WSClient("127.0.0.1", port);

            try
            {
                Assert.IsTrue(client.Connect(15000), "明文 WebSocket 握手应在超时前完成。");

                Assert.ThrowsException<InvalidOperationException>(
                    () => client.ConfigurePipeline = (id, pipeline) => { });
            }
            finally
            {
                client.Close();
                server.Stop();
            }
        }

        private static int GetFreePort()
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