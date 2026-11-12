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
*命名空间：SAEA.RPC.Tests
*文件名： FilterChannelTests
*版本号： net10.0
*唯一标识：c1f0c6d0-2d63-4b2f-8f6b-3a1c40f2a8bd
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 16:45:50
*描述：校验过滤器收到连接视图且客户端地址已写入通道属性
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 16:45:50
*修改人： yswenli
*版本号： net10.0
*描述：新增，校验 IChannel 参数与 RemoteEndPoint 通道属性
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.RPC.Consumer;
using SAEA.RPC.Provider;
using SAEA.Sockets.Pipeline;

namespace SAEA.RPC.Tests
{
    /// <summary>
    /// 过滤器入参从 IUserToken 收敛为 IChannel 后的行为：连接视图非空、标识非空且带客户端地址属性。
    /// </summary>
    [TestClass]
    public class FilterChannelTests
    {
        /// <summary>
        /// 本机回环调用一次 RPC，过滤器应收到带标识和客户端地址的连接视图。
        /// </summary>
        [TestMethod]
        public void ActionFilterAtrribute_RemoteCall_ReceivesChannelWithRemoteEndPoint()
        {
            var port = GetFreePort();

            CapturingFilterAttribute.Reset();

            var provider = new ServiceProvider(new[] { typeof(FilterProbeService) }, port);
            provider.Start();

            try
            {
                using (var consumer = new ServiceConsumer(new Uri("rpc://127.0.0.1:" + port), 1, 1, 5000))
                {
                    Assert.AreEqual("hi", consumer.RemoteCall<string>("FilterProbeService", "Echo", "hi"));
                }

                var channel = CapturingFilterAttribute.Channel;

                Assert.IsNotNull(channel);
                Assert.IsFalse(string.IsNullOrEmpty(channel.Id));
                Assert.IsInstanceOfType(channel.GetAttribute(RpcChannelKeys.RemoteEndPoint), typeof(EndPoint));
            }
            finally
            {
                provider.Stop();
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

    /// <summary>
    /// 测试服务：唯一方法用于触发过滤器并回显入参。
    /// </summary>
    [RPCService]
    public class FilterProbeService
    {
        /// <summary>
        /// 回显入参
        /// </summary>
        /// <param name="text">待回显的文本</param>
        /// <returns>原样返回的文本</returns>
        [CapturingFilter]
        public string Echo(string text)
        {
            return text;
        }
    }

    /// <summary>
    /// 捕获过滤器：记录服务端收到的连接视图，供用例断言。
    /// 静态字段是因为过滤器实例由服务映射表持有，用例只能从外部读取。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class CapturingFilterAttribute : ActionFilterAtrribute
    {
        private static IChannel _channel;

        /// <summary>最近一次调用收到的连接视图。</summary>
        public static IChannel Channel
        {
            get { return _channel; }
        }

        /// <summary>清空上一次用例留下的捕获结果。</summary>
        public static void Reset()
        {
            _channel = null;
        }

        /// <inheritdoc />
        public override bool OnActionExecuting(IChannel channel, string serviceName, string methodName, object[] args)
        {
            _channel = channel;
            return true;
        }

        /// <inheritdoc />
        public override void OnActionExecuted(IChannel channel, string serviceName, string methodName, object[] args, object result)
        {
        }
    }
}