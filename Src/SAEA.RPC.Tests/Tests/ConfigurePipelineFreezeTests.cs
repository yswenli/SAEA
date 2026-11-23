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
*命名空间：SAEA.RPC.Tests
*文件名： ConfigurePipelineFreezeTests
*版本号： net10.0
*唯一标识：2c8b3b7e-eabb-4556-a364-c291d364160d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 10:58:00
*描述：RPC 服务端与客户端装配回调冻结的校验用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 10:58:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，校验 Start/构造之后再赋值装配回调会抛异常
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.RPC.Consumer;
using SAEA.RPC.Provider;

namespace SAEA.RPC.Tests
{
    /// <summary>
    /// 处理器链装配回调的冻结语义：服务端 Start 之前可赋值，客户端因构造期建链只能构造注入，之后赋值必须抛异常。
    /// </summary>
    [TestClass]
    public class ConfigurePipelineFreezeTests
    {
        /// <summary>
        /// 服务端启动后再赋值装配回调应抛 InvalidOperationException。
        /// </summary>
        [TestMethod]
        public void ServiceProvider_ConfigurePipelineAfterStart_Throws()
        {
            var port = GetFreePort();

            var provider = CreateProvider(port);

            provider.Start();

            try
            {
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => provider.ConfigurePipeline = (id, pipeline) => { });
            }
            finally
            {
                provider.Stop();
            }
        }

        /// <summary>
        /// 消费者在构造期建立连接，构造完成后再赋值装配回调应抛 InvalidOperationException。
        /// </summary>
        [TestMethod]
        public void ServiceConsumer_ConfigurePipelineAfterConstruction_Throws()
        {
            var port = GetFreePort();

            var provider = CreateProvider(port);

            provider.Start();

            try
            {
                var consumer = new ServiceConsumer(new Uri("rpc://127.0.0.1:" + port), 1, 1, 5000);

                try
                {
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => consumer.ConfigurePipeline = (id, pipeline) => { });
                }
                finally
                {
                    consumer.Dispose();
                }
            }
            finally
            {
                provider.Stop();
            }
        }

        private static ServiceProvider CreateProvider(int port)
        {
            // 传入测试类型本身，避免空数组触发按调用栈扫描全部服务。
            return new ServiceProvider(new[] { typeof(ConfigurePipelineFreezeTests) }, port);
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