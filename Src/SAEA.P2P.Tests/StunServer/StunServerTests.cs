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
*命名空间：SAEA.P2P.Tests.StunServer
*文件名： StunServerTests
*版本号： net10.0
*唯一标识：b04402a0-9c2c-435d-a09d-33591c857e91
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 19:30:00
*描述：STUN 服务端 Binding 应答、换端口响应、未知属性与指示静默验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 19:30:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 STUN 服务端用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Stun;
using SAEA.P2P.StunServer;
using SAEA.P2P.Transport;

using StunServerHost = SAEA.P2P.StunServer.StunServer;

namespace SAEA.P2P.Tests.StunServer
{
    /// <summary>
    /// STUN 服务端：Binding 请求的映射地址与响应来源、CHANGE-REQUEST 换端口、未知必选属性 420、指示不响应。
    /// </summary>
    [TestClass]
    public class StunServerTests
    {
        /// <summary>
        /// 测试统一使用 IPv4 回环地址
        /// </summary>
        private static readonly IPAddress Host = IPAddress.Loopback;

        /// <summary>
        /// 单端点部署时成功应答应携带异或映射地址、映射地址、响应来源与 SOFTWARE
        /// </summary>
        [TestMethod]
        [Timeout(15000)]
        public async Task StunServer_绑定请求_响应含xor映射地址()
        {
            using (var server = new StunServerHost(new StunServerOptions(new IPEndPoint(Host, 0)) { Software = "stun-server-test" }))
            using (var transport = new SaeaUdpTransport())
            {
                await server.StartAsync();
                await transport.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);

                var request = new StunMessage(StunMessageType.Binding, StunClass.Request);
                var exchange = await ExchangeAsync(transport, server.Endpoints[0].LocalEndPoint, request, TimeSpan.FromSeconds(5));

                Assert.IsNotNull(exchange.Response, "服务端应回应答");
                Assert.AreEqual(StunClass.SuccessResponse, exchange.Response.Class, "应返回成功应答");
                Assert.AreEqual(server.Endpoints[0].LocalEndPoint, exchange.From, "应答应来自主监听端点");

                var xorMapped = DecodeAddress(exchange.Response, StunAttributeType.XorMappedAddress);
                Assert.IsNotNull(xorMapped, "响应应携带 XOR-MAPPED-ADDRESS");
                Assert.AreEqual(transport.LocalEndPoint, xorMapped, "异或映射地址应为客户端本地端点");

                var mapped = DecodeAddress(exchange.Response, StunAttributeType.MappedAddress);
                Assert.IsNotNull(mapped, "响应应携带 MAPPED-ADDRESS");
                Assert.AreEqual(transport.LocalEndPoint, mapped, "映射地址应为客户端本地端点");

                var responseOrigin = DecodeAddress(exchange.Response, StunAttributeType.ResponseOrigin);
                Assert.IsNotNull(responseOrigin, "响应应携带 RESPONSE-ORIGIN");
                Assert.AreEqual(server.Endpoints[0].LocalEndPoint, responseOrigin, "响应来源应为实际发送端点");

                StunAttribute other;
                Assert.IsFalse(exchange.Response.TryGet(StunAttributeType.OtherAddress, out other), "单端点部署不得编造 OTHER-ADDRESS");
                Assert.AreEqual("stun-server-test", ReadString(exchange.Response, StunAttributeType.Software), "应携带配置的 SOFTWARE");
            }
        }

        /// <summary>
        /// 请求 CHANGE-REQUEST 换端口时，应答应从备用端口发出，RESPONSE-ORIGIN 与 OTHER-ADDRESS 随之切换
        /// </summary>
        [TestMethod]
        [Timeout(15000)]
        public async Task StunServer_CHANGE_REQUEST换端口_响应用另一端口发出()
        {
            var ports = ReserveUdpPorts(2);
            var primary = new IPEndPoint(Host, ports[0]);
            var alternate = new IPEndPoint(Host, ports[1]);

            using (var server = new StunServerHost(new StunServerOptions(primary, alternate)))
            using (var transport = new SaeaUdpTransport())
            {
                await server.StartAsync();
                await transport.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);

                StunServerEndpoint primaryEndpoint = null;
                for (var i = 0; i < server.Endpoints.Count; i++)
                {
                    if (server.Endpoints[i].LocalEndPoint.Port == primary.Port)
                    {
                        primaryEndpoint = server.Endpoints[i];
                    }
                }
                Assert.IsNotNull(primaryEndpoint, "应绑定主端口");

                var request = new StunMessage(StunMessageType.Binding, StunClass.Request);
                request.SetAttributes(new[]
                {
                    StunAttributeCodec.CreateUInt32(StunAttributeType.ChangeRequest, 0x02)
                });

                var exchange = await ExchangeAsync(transport, primaryEndpoint.LocalEndPoint, request, TimeSpan.FromSeconds(5));

                Assert.IsNotNull(exchange.Response, "服务端应回应答");
                Assert.AreEqual(StunClass.SuccessResponse, exchange.Response.Class, "换端口请求应成功");
                Assert.AreEqual(alternate, exchange.From, "应答应从备用端口发出");

                var responseOrigin = DecodeAddress(exchange.Response, StunAttributeType.ResponseOrigin);
                Assert.AreEqual(alternate, responseOrigin, "RESPONSE-ORIGIN 应为备用端点");

                var otherAddress = DecodeAddress(exchange.Response, StunAttributeType.OtherAddress);
                Assert.AreEqual(alternate, otherAddress, "OTHER-ADDRESS 应为相对主端点的备用端点");
            }
        }

        /// <summary>
        /// 请求携带未知 comprehension-required 属性时返回 420，并在 UNKNOWN-ATTRIBUTES 中列出该类型
        /// </summary>
        [TestMethod]
        [Timeout(15000)]
        public async Task StunServer_未知必选属性_返回420带列表()
        {
            const ushort unknownType = 0x000F;

            using (var server = new StunServerHost(new StunServerOptions(new IPEndPoint(Host, 0))))
            using (var transport = new SaeaUdpTransport())
            {
                await server.StartAsync();
                await transport.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);

                var request = new StunMessage(StunMessageType.Binding, StunClass.Request);
                request.SetAttributes(new[] { new StunAttribute(unknownType, new byte[0]) });

                var exchange = await ExchangeAsync(transport, server.Endpoints[0].LocalEndPoint, request, TimeSpan.FromSeconds(5));

                Assert.IsNotNull(exchange.Response, "服务端应回应答");
                Assert.AreEqual(StunClass.ErrorResponse, exchange.Response.Class, "未知必选属性应返回错误应答");

                int code;
                string reason;
                Assert.IsTrue(StunErrorCode.TryGet(exchange.Response, out code, out reason), "错误应答应携带 ERROR-CODE");
                Assert.AreEqual(420, code, "错误码应为 420");

                StunAttribute unknown;
                Assert.IsTrue(exchange.Response.TryGet(StunAttributeType.UnknownAttributes, out unknown), "应携带 UNKNOWN-ATTRIBUTES");
                var value = unknown.Value.ToArray();
                Assert.IsTrue(value.Length >= 2, "UNKNOWN-ATTRIBUTES 至少包含一个类型");
                Assert.AreEqual(unknownType, (ushort)((value[0] << 8) | value[1]), "列表中应包含请求里的未知类型");
            }
        }

        /// <summary>
        /// Binding indication 没有事务，服务端必须静默丢弃
        /// </summary>
        [TestMethod]
        [Timeout(15000)]
        public async Task StunServer_indication_不响应()
        {
            using (var server = new StunServerHost(new StunServerOptions(new IPEndPoint(Host, 0))))
            using (var transport = new SaeaUdpTransport())
            {
                await server.StartAsync();
                await transport.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);

                var received = new CountBox();
                Action<IPEndPoint, ReadOnlyMemory<byte>> handler = (from, data) => received.Value = received.Value + 1;
                transport.DatagramReceived += handler;
                try
                {
                    var indication = new StunMessage(StunMessageType.Binding, StunClass.Indication);
                    await transport.SendToAsync(
                        server.Endpoints[0].LocalEndPoint,
                        new ReadOnlyMemory<byte>(indication.ToArray()),
                        CancellationToken.None);

                    await Task.Delay(500);
                    Assert.AreEqual(0, received.Value, "指示不得触发任何应答");
                }
                finally
                {
                    transport.DatagramReceived -= handler;
                }
            }
        }

        /// <summary>
        /// 请求带 FINGERPRINT 时响应也应带 FINGERPRINT 且校验通过
        /// </summary>
        [TestMethod]
        [Timeout(15000)]
        public async Task StunServer_请求带指纹_响应带指纹()
        {
            using (var server = new StunServerHost(new StunServerOptions(new IPEndPoint(Host, 0))))
            using (var transport = new SaeaUdpTransport())
            {
                await server.StartAsync();
                await transport.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);

                var request = new StunMessage(StunMessageType.Binding, StunClass.Request);
                StunFingerprint.Append(request);

                var exchange = await ExchangeAsync(transport, server.Endpoints[0].LocalEndPoint, request, TimeSpan.FromSeconds(5));

                Assert.IsNotNull(exchange.Response, "服务端应回应答");
                StunAttribute fingerprint;
                Assert.IsTrue(exchange.Response.TryGet(StunAttributeType.Fingerprint, out fingerprint), "响应应携带 FINGERPRINT");
                Assert.IsTrue(StunFingerprint.TryVerify(exchange.Response), "响应 FINGERPRINT 应校验通过");
            }
        }

        /// <summary>
        /// 发请求并等待第一条能解析的应答；超时返回空结果
        /// </summary>
        /// <param name="transport">客户端传输</param>
        /// <param name="target">服务端端点</param>
        /// <param name="request">请求</param>
        /// <param name="timeout">等待上限</param>
        /// <returns>应答与来源端点</returns>
        private static async Task<ExchangeResult> ExchangeAsync(IUdpTransport transport, IPEndPoint target, StunMessage request, TimeSpan timeout)
        {
            var completion = new TaskCompletionSource<ExchangeResult>();
            Action<IPEndPoint, ReadOnlyMemory<byte>> handler = (from, data) =>
            {
                StunMessage message;
                StunParseError error;
                if (!StunMessage.TryParse(data.Span, out message, out error))
                {
                    return;
                }

                completion.TrySetResult(new ExchangeResult { Response = message, From = from });
            };

            transport.DatagramReceived += handler;
            try
            {
                await transport.SendToAsync(target, new ReadOnlyMemory<byte>(request.ToArray()), CancellationToken.None);
                var delay = Task.Delay(timeout);
                var completed = await Task.WhenAny(completion.Task, delay);
                if (completed != completion.Task)
                {
                    return new ExchangeResult();
                }

                return await completion.Task;
            }
            finally
            {
                transport.DatagramReceived -= handler;
            }
        }

        /// <summary>
        /// 解码地址属性；缺失或格式错误时返回 null
        /// </summary>
        /// <param name="message">应答</param>
        /// <param name="type">属性类型</param>
        /// <returns>解码出的端点</returns>
        private static IPEndPoint DecodeAddress(StunMessage message, StunAttributeType type)
        {
            StunAttribute attribute;
            if (!message.TryGet(type, out attribute))
            {
                return null;
            }

            IPEndPoint endPoint;
            return StunAddressCodec.TryDecodeAddress(attribute, message.TransactionId.Span, out endPoint) ? endPoint : null;
        }

        /// <summary>
        /// 读取字符串属性；缺失时返回 null
        /// </summary>
        /// <param name="message">应答</param>
        /// <param name="type">属性类型</param>
        /// <returns>UTF-8 字符串</returns>
        private static string ReadString(StunMessage message, StunAttributeType type)
        {
            StunAttribute attribute;
            if (!message.TryGet(type, out attribute))
            {
                return null;
            }

            return System.Text.Encoding.UTF8.GetString(attribute.Value.ToArray());
        }

        /// <summary>
        /// 预占若干个尚未使用的 UDP 端口；同时持有套接字可避免返回重复端口
        /// </summary>
        /// <param name="count">端口数量</param>
        /// <returns>端口数组</returns>
        private static int[] ReserveUdpPorts(int count)
        {
            var probes = new List<SaeaUdpTransport>();
            try
            {
                var ports = new int[count];
                for (var i = 0; i < count; i++)
                {
                    var probe = new SaeaUdpTransport();
                    probe.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None).GetAwaiter().GetResult();
                    probes.Add(probe);
                    ports[i] = probe.LocalEndPoint.Port;
                }
                return ports;
            }
            finally
            {
                for (var i = 0; i < probes.Count; i++)
                {
                    probes[i].Dispose();
                }
            }
        }

        /// <summary>
        /// 可跨接收回调累加的计数器
        /// </summary>
        private sealed class CountBox
        {
            /// <summary>
            /// 当前计数
            /// </summary>
            public int Value;
        }

        /// <summary>
        /// 一次请求应答的结果
        /// </summary>
        private sealed class ExchangeResult
        {
            /// <summary>
            /// 解析后的应答；超时为 null
            /// </summary>
            public StunMessage Response { get; set; }

            /// <summary>
            /// 应答来源端点；超时为 null
            /// </summary>
            public IPEndPoint From { get; set; }
        }
    }
}