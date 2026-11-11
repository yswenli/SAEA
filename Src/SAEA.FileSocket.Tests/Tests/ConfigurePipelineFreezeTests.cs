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
*命名空间：SAEA.FileSocket.Tests
*文件名： ConfigurePipelineFreezeTests
*版本号： net10.0
*唯一标识：3a6f3fba-2133-4242-ad7b-39af52bdfc6c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 10:58:00
*描述：文件服务端启动后装配回调冻结的校验用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 10:58:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，校验 Start 之后再赋值装配回调会抛异常
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.FileSocket;

namespace SAEA.FileSocket.Tests
{
    /// <summary>
    /// 处理器链装配回调的冻结语义：Start 之前可赋值，启动后再赋值必须抛异常。
    /// </summary>
    [TestClass]
    public class ConfigurePipelineFreezeTests
    {
        /// <summary>
        /// 服务端启动后再赋值装配回调应抛 InvalidOperationException。
        /// </summary>
        [TestMethod]
        public void Server_ConfigurePipelineAfterStart_Throws()
        {
            var port = GetFreePort();

            var server = new Server(port);

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