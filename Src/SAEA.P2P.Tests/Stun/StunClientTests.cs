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
*命名空间：SAEA.P2P.Tests.Stun
*文件名： StunClientTests
*版本号： net10.0
*唯一标识：82882745-d94b-422e-becc-0f126691fd1a
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 18:00:00
*描述：STUN 客户端正常应答、凭据重试与超时验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 18:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 STUN 客户端用例
*
*****************************************************************************/
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Stun;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Tests.Stun
{
    /// <summary>
    /// STUN 客户端：正常应答解析映射地址、401 挑战后用长期凭据重试、无应答时按 RTO 判超时。
    /// </summary>
    [TestClass]
    public class StunClientTests
    {
        /// <summary>
        /// 挑战用的认证域
        /// </summary>
        private const string ChallengeRealm = "example.org";

        /// <summary>
        /// 挑战用的一次性随机串
        /// </summary>
        private const string ChallengeNonce = "f//499k954d6OL34oL9FSTvy64sA";

        /// <summary>
        /// 服务端成功应答：客户端应拿到映射地址、响应来源与备用地址
        /// </summary>
        [TestMethod]
        [Timeout(15000)]
        public async Task StunClient_正常响应_返回映射地址()
        {
            using (var server = new FakeStunServer((request, remote, local) => BuildSuccess(request, remote, local)))
            using (var transport = new SaeaUdpTransport())
            using (var client = new StunClient(transport))
            {
                await transport.BindAsync(new IPEndPoint(IPAddress.Loopback, 0), CancellationToken.None);

                var result = await client.BindingAsync(server.EndPoint);

                Assert.IsTrue(result.Succeeded, "正常应答应视为成功");
                Assert.AreEqual(StunBindingFailure.None, result.Failure, "成功时不应有失败原因");
                Assert.IsNotNull(result.XorMappedAddress, "应解析出 XOR-MAPPED-ADDRESS");
                Assert.AreEqual(transport.LocalEndPoint, result.XorMappedAddress, "映射地址应为客户端本地端点");
                Assert.AreEqual(transport.LocalEndPoint, result.MappedAddress, "MAPPED-ADDRESS 也应解析成功");
                Assert.AreEqual(server.EndPoint, result.ResponseOrigin, "响应来源应为服务端监听端点");
                Assert.IsNotNull(result.OtherAddress, "应解析出 OTHER-ADDRESS");
                Assert.AreEqual(1, result.Attempts, "一次成功应答只应发送一次");
                Assert.IsTrue(result.Elapsed >= TimeSpan.Zero, "耗时不能为负");
                Assert.AreEqual(1, server.RequestCount, "服务端应只收到一次请求");
            }
        }

        /// <summary>
        /// 服务端先回 401，客户端取挑战里的 realm 与 nonce 重试，第二次带合法 MIC
        /// </summary>
        [TestMethod]
        [Timeout(15000)]
        public async Task StunClient_401一次_用新nonce重试成功()
        {
            var authenticated = false;
            var challengeSent = false;

            using (var server = new FakeStunServer((request, remote, local) =>
            {
                StunAttribute integrity;
                if (!request.TryGet(StunAttributeType.MessageIntegrity, out integrity))
                {
                    challengeSent = true;
                    return BuildChallenge(request);
                }

                // 长期凭据必须用 MD5(username:realm:password) 派生密钥
                var key = StunIntegrity.CreateLongTermKey(
                    ReadString(request, StunAttributeType.Username),
                    ReadString(request, StunAttributeType.Realm),
                    "s3cret");
                authenticated = StunIntegrity.TryVerify(request, key);
                return BuildSuccess(request, remote, local);
            }))
            using (var transport = new SaeaUdpTransport())
            using (var client = new StunClient(transport))
            {
                await transport.BindAsync(new IPEndPoint(IPAddress.Loopback, 0), CancellationToken.None);

                var credential = new StunClientCredential("alice", "s3cret");
                var result = await client.BindingAsync(server.EndPoint, credential);

                Assert.IsTrue(challengeSent, "服务端应先发出 401 挑战");
                Assert.AreEqual(2, server.RequestCount, "应恰好重试一次");
                Assert.IsTrue(authenticated, "重试请求的 MESSAGE-INTEGRITY 应校验通过");
                Assert.IsTrue(result.Succeeded, "带凭据重试后应成功");
                Assert.AreEqual(2, result.Attempts, "两次请求各发送一次");
            }
        }

        /// <summary>
        /// 服务端不回应答时按 RTO 退避重传，最终返回超时而不是抛异常
        /// </summary>
        [TestMethod]
        [Timeout(15000)]
        public async Task StunClient_超时_返回超时错误码()
        {
            using (var server = new FakeStunServer((request, remote, local) => null))
            using (var transport = new SaeaUdpTransport())
            using (var client = new StunClient(transport))
            {
                await transport.BindAsync(new IPEndPoint(IPAddress.Loopback, 0), CancellationToken.None);

                // 把 RTO 压到 5ms，总超时约 395ms，避免用例真等 39.5 秒
                client.RtoMilliseconds = 5;

                var result = await client.BindingAsync(server.EndPoint);

                Assert.IsFalse(result.Succeeded, "无应答不应视为成功");
                Assert.AreEqual(StunBindingFailure.Timeout, result.Failure, "失败原因应为超时");
                Assert.AreEqual(0, result.ErrorCode, "超时没有 STUN 错误码");
                Assert.IsNull(result.MappedAddress, "超时不应带回映射地址");
                Assert.AreEqual(StunTransactionTiming.TotalSendCount, result.Attempts, "超时前应发满 7 次");
            }
        }

        /// <summary>
        /// 构造 401 挑战应答，携带 REALM 与 NONCE
        /// </summary>
        /// <param name="request">客户端请求</param>
        /// <returns>应答字节</returns>
        private static byte[] BuildChallenge(StunMessage request)
        {
            var response = new StunMessage(StunMessageType.Binding, StunClass.ErrorResponse, request.TransactionId.ToArray());
            response.SetAttributes(new[]
            {
                StunErrorCode.Create(401, "Unauthorized"),
                new StunAttribute(StunAttributeType.Realm, Encoding.UTF8.GetBytes(ChallengeRealm)),
                new StunAttribute(StunAttributeType.Nonce, Encoding.UTF8.GetBytes(ChallengeNonce))
            });
            return response.ToArray();
        }

        /// <summary>
        /// 构造成功应答，携带异或映射地址、映射地址、响应来源与备用地址
        /// </summary>
        /// <param name="request">客户端请求</param>
        /// <param name="remote">客户端来源端点</param>
        /// <param name="local">服务端监听端点</param>
        /// <returns>应答字节</returns>
        private static byte[] BuildSuccess(StunMessage request, IPEndPoint remote, IPEndPoint local)
        {
            var transactionId = request.TransactionId.ToArray();
            var response = new StunMessage(StunMessageType.Binding, StunClass.SuccessResponse, transactionId);
            response.SetAttributes(new[]
            {
                StunAddressCodec.EncodeAddress(StunAttributeType.XorMappedAddress, remote, true, transactionId),
                StunAddressCodec.EncodeAddress(StunAttributeType.MappedAddress, remote, false),
                StunAddressCodec.EncodeAddress(StunAttributeType.ResponseOrigin, local, false),
                StunAddressCodec.EncodeAddress(StunAttributeType.OtherAddress, new IPEndPoint(IPAddress.Loopback, 3479), false)
            });
            return response.ToArray();
        }

        /// <summary>
        /// 读取字符串属性；缺失时返回 null
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="type">属性类型</param>
        /// <returns>UTF-8 字符串</returns>
        private static string ReadString(StunMessage message, StunAttributeType type)
        {
            StunAttribute attribute;
            if (!message.TryGet(type, out attribute))
            {
                return null;
            }
            return Encoding.UTF8.GetString(attribute.Value.ToArray());
        }

        /// <summary>
        /// 用户态假 STUN 服务端：处理函数返回 null 表示不回应答
        /// </summary>
        private sealed class FakeStunServer : IDisposable
        {
            /// <summary>
            /// 底层 UDP 传输
            /// </summary>
            private readonly SaeaUdpTransport _transport = new SaeaUdpTransport();

            /// <summary>
            /// 请求处理函数
            /// </summary>
            private readonly Func<StunMessage, IPEndPoint, IPEndPoint, byte[]> _handler;

            /// <summary>
            /// 收到的请求数
            /// </summary>
            private int _requestCount;

            /// <summary>
            /// 绑定回环随机端口并开始接收
            /// </summary>
            /// <param name="handler">请求处理函数</param>
            public FakeStunServer(Func<StunMessage, IPEndPoint, IPEndPoint, byte[]> handler)
            {
                _handler = handler;
                _transport.BindAsync(new IPEndPoint(IPAddress.Loopback, 0), CancellationToken.None).GetAwaiter().GetResult();
                _transport.DatagramReceived += OnDatagramReceived;
            }

            /// <summary>
            /// 服务端端点
            /// </summary>
            public IPEndPoint EndPoint
            {
                get { return _transport.LocalEndPoint; }
            }

            /// <summary>
            /// 收到的请求数
            /// </summary>
            public int RequestCount
            {
                get { return Interlocked.CompareExchange(ref _requestCount, 0, 0); }
            }

            /// <summary>
            /// 退订并释放传输
            /// </summary>
            public void Dispose()
            {
                _transport.DatagramReceived -= OnDatagramReceived;
                _transport.Dispose();
            }

            /// <summary>
            /// 解析请求并交处理函数；处理函数返回 null 时不回应答
            /// </summary>
            /// <param name="remoteEndPoint">客户端来源</param>
            /// <param name="data">数据报内容</param>
            private void OnDatagramReceived(IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> data)
            {
                StunMessage request;
                StunParseError error;
                if (!StunMessage.TryParse(data.Span, out request, out error))
                {
                    return;
                }

                Interlocked.Increment(ref _requestCount);
                var response = _handler(request, remoteEndPoint, _transport.LocalEndPoint);
                if (response == null)
                {
                    return;
                }

                _transport.SendToAsync(remoteEndPoint, new ReadOnlyMemory<byte>(response), CancellationToken.None).GetAwaiter().GetResult();
            }
        }
    }
}