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
*命名空间：SAEA.P2P.Tests.Ice
*文件名： IceInboundResponderTests
*版本号： net10.0
*唯一标识：36adfeca-38eb-442a-b564-84720682f924
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 14:31:40
*描述：UDP 入向 ICE 检查应答器用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 15:00:08
*修改人： yswenli
*版本号： net10.0
*描述：USERNAME 采用冒号前为本端 ufrag 的约定
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Ice;
using SAEA.P2P.Stun;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// UDP 入向检查应答器：成功应答、提名标记与 USERNAME 不匹配时的静默丢弃。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
    [TestClass]
    public class IceInboundResponderTests
    {
        /// <summary>
        /// 本端 ufrag
        /// </summary>
        private const string LocalUfrag = "localUfrag";

        /// <summary>
        /// 本端短凭据口令
        /// </summary>
        private const string LocalPassword = "localPwd";

        /// <summary>
        /// 用例使用的检查优先级
        /// </summary>
        private const uint CheckPriority = 0x7E0000FF;

        /// <summary>
        /// 等待应答的上限，避免底层不回调时挂住整个测试进程
        /// </summary>
        private const int WaitMilliseconds = 2000;

        /// <summary>
        /// 有效检查应回带 XOR-MAPPED-ADDRESS 与事务 ID 的成功响应，并把来源与 PRIORITY 上报给会话
        /// </summary>
        [TestMethod]
        [Timeout(15000)]
        public async Task IceInboundResponder_有效检查_回成功响应()
        {
            using (var responderTransport = new SaeaUdpTransport())
            using (var peerTransport = new SaeaUdpTransport())
            {
                await BindAsync(responderTransport).ConfigureAwait(false);
                await BindAsync(peerTransport).ConfigureAwait(false);

                var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                peerTransport.DatagramReceived += (from, data) => received.TrySetResult(data.ToArray());

                var eventTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var eventCount = 0;
                IUdpTransport eventTransport = null;
                IPEndPoint eventSource = null;
                uint eventPriority = 0;
                bool eventUseCandidate = true;

                using (var responder = new IceInboundResponder(new IUdpTransport[] { responderTransport }, LocalUfrag, LocalPassword))
                {
                    responder.CheckRequestReceived += (transport, source, priority, useCandidate) =>
                    {
                        eventTransport = transport;
                        eventSource = source;
                        eventPriority = priority;
                        eventUseCandidate = useCandidate;
                        Interlocked.Increment(ref eventCount);
                        eventTcs.TrySetResult(true);
                    };
                    responder.Start();

                    var peerEndPoint = peerTransport.LocalEndPoint;
                    var request = BuildRequest(LocalUfrag + ":peerUfrag", CheckPriority, false);
                    var requestBytes = SignRequest(request, LocalPassword);

                    await peerTransport.SendToAsync(responderTransport.LocalEndPoint, requestBytes, CancellationToken.None).ConfigureAwait(false);

                    var payload = await ReceiveAsync(received).ConfigureAwait(false);
                    var response = ParseResponse(payload);

                    Assert.AreEqual(StunClass.SuccessResponse, response.Class);
                    Assert.AreEqual(StunMessageType.Binding, response.Method);
                    CollectionAssert.AreEqual(request.TransactionId.ToArray(), response.TransactionId.ToArray());

                    // 应答必须能通过短凭据校验，否则对端会把成功响应当成伪造包丢弃
                    Assert.IsTrue(StunIntegrity.TryVerify(response, StunIntegrity.CreateShortTermKey(LocalPassword)));

                    StunAttribute xorMapped;
                    Assert.IsTrue(response.TryGet(StunAttributeType.XorMappedAddress, out xorMapped));
                    Assert.AreEqual(peerEndPoint, DecodeMappedAddress(response, xorMapped));

                    var eventCompleted = await Task.WhenAny(eventTcs.Task, Task.Delay(WaitMilliseconds)).ConfigureAwait(false);
                    Assert.AreSame(eventTcs.Task, eventCompleted, "未触发检查请求事件");
                    Assert.AreEqual(1, Volatile.Read(ref eventCount));
                    Assert.AreSame(responderTransport, eventTransport);
                    Assert.AreEqual(peerEndPoint, eventSource);
                    Assert.AreEqual(CheckPriority, eventPriority);
                    Assert.IsFalse(eventUseCandidate);

                    Assert.AreEqual(1, responder.HandledRequestCount);
                }
            }
        }

        /// <summary>
        /// 带 USE-CANDIDATE 的检查要在上报时标出提名意图
        /// </summary>
        [TestMethod]
        [Timeout(15000)]
        public async Task IceInboundResponder_带USE_CANDIDATE_事件标记提名()
        {
            using (var responderTransport = new SaeaUdpTransport())
            using (var peerTransport = new SaeaUdpTransport())
            {
                await BindAsync(responderTransport).ConfigureAwait(false);
                await BindAsync(peerTransport).ConfigureAwait(false);

                var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                peerTransport.DatagramReceived += (from, data) => received.TrySetResult(data.ToArray());

                var eventTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var nominated = false;

                using (var responder = new IceInboundResponder(new IUdpTransport[] { responderTransport }, LocalUfrag, LocalPassword))
                {
                    responder.CheckRequestReceived += (transport, source, priority, useCandidate) =>
                    {
                        nominated = useCandidate;
                        eventTcs.TrySetResult(true);
                    };
                    responder.Start();

                    var request = BuildRequest(LocalUfrag + ":peerUfrag", CheckPriority, true);
                    var requestBytes = SignRequest(request, LocalPassword);

                    await peerTransport.SendToAsync(responderTransport.LocalEndPoint, requestBytes, CancellationToken.None).ConfigureAwait(false);

                    var payload = await ReceiveAsync(received).ConfigureAwait(false);
                    var response = ParseResponse(payload);
                    Assert.AreEqual(StunClass.SuccessResponse, response.Class);

                    var eventCompleted = await Task.WhenAny(eventTcs.Task, Task.Delay(WaitMilliseconds)).ConfigureAwait(false);
                    Assert.AreSame(eventTcs.Task, eventCompleted, "未触发检查请求事件");
                    Assert.IsTrue(nominated, "USE-CANDIDATE 检查未标记为提名");

                    Assert.AreEqual(1, responder.HandledRequestCount);
                }
            }
        }

        /// <summary>
        /// USERNAME 冒号前的本端 ufrag 不符时既不回包也不上报，避免向对端暴露口令线索
        /// </summary>
        [TestMethod]
        [Timeout(15000)]
        public async Task IceInboundResponder_USERNAME不匹配_不回包()
        {
            using (var responderTransport = new SaeaUdpTransport())
            using (var peerTransport = new SaeaUdpTransport())
            {
                await BindAsync(responderTransport).ConfigureAwait(false);
                await BindAsync(peerTransport).ConfigureAwait(false);

                var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                peerTransport.DatagramReceived += (from, data) => received.TrySetResult(data.ToArray());

                var eventTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                using (var responder = new IceInboundResponder(new IUdpTransport[] { responderTransport }, LocalUfrag, LocalPassword))
                {
                    responder.CheckRequestReceived += (transport, source, priority, useCandidate) => eventTcs.TrySetResult(true);
                    responder.Start();

                    // 口令正确但 USERNAME 指向别的 ufrag，必须按凭据不符静默丢弃
                    var request = BuildRequest("otherUfrag:peerUfrag", CheckPriority, false);
                    var requestBytes = SignRequest(request, LocalPassword);

                    await peerTransport.SendToAsync(responderTransport.LocalEndPoint, requestBytes, CancellationToken.None).ConfigureAwait(false);

                    await Task.Delay(500).ConfigureAwait(false);

                    Assert.IsFalse(received.Task.IsCompleted, "USERNAME 不匹配时不应回包");
                    Assert.IsFalse(eventTcs.Task.IsCompleted, "USERNAME 不匹配时不应触发事件");
                    Assert.AreEqual(0, responder.HandledRequestCount);
                }
            }
        }

        /// <summary>
        /// 绑定回环地址并开始接收
        /// </summary>
        /// <param name="transport">待绑定的传输</param>
        /// <returns>绑定完成的任务</returns>
        private static async Task BindAsync(SaeaUdpTransport transport)
        {
            await transport.BindAsync(new IPEndPoint(IPAddress.Loopback, 0), CancellationToken.None).ConfigureAwait(false);
            transport.StartReceive();
        }

        /// <summary>
        /// 构造检查请求属性，顺序固定为 USERNAME、PRIORITY 与可选的 USE-CANDIDATE
        /// </summary>
        /// <param name="userName">USERNAME 属性值</param>
        /// <param name="priority">PRIORITY 属性值</param>
        /// <param name="useCandidate">是否携带 USE-CANDIDATE</param>
        /// <returns>未签名的检查请求</returns>
        private static StunMessage BuildRequest(string userName, uint priority, bool useCandidate)
        {
            var request = new StunMessage(StunMessageType.Binding, StunClass.Request);
            var attributes = new List<StunAttribute>
            {
                new StunAttribute(StunAttributeType.Username, Encoding.UTF8.GetBytes(userName)),
                StunAttributeCodec.CreateUInt32(StunAttributeType.Priority, priority)
            };
            if (useCandidate) attributes.Add(StunAttributeCodec.CreateEmpty(StunAttributeType.UseCandidate));
            request.SetAttributes(attributes);
            return request;
        }

        /// <summary>
        /// 追加 MESSAGE-INTEGRITY 与 FINGERPRINT 后编码
        /// </summary>
        /// <param name="request">待签名的检查请求，会被就地修改</param>
        /// <param name="password">短凭据口令</param>
        /// <returns>签名后的报文字节</returns>
        private static byte[] SignRequest(StunMessage request, string password)
        {
            StunIntegrity.Append(request, StunIntegrity.CreateShortTermKey(password));
            StunFingerprint.Append(request);
            return request.ToArray();
        }

        /// <summary>
        /// 等待对端数据报，超时直接判失败
        /// </summary>
        /// <param name="source">接收完成源</param>
        /// <returns>收到的报文字节</returns>
        private static async Task<byte[]> ReceiveAsync(TaskCompletionSource<byte[]> source)
        {
            var completed = await Task.WhenAny(source.Task, Task.Delay(WaitMilliseconds)).ConfigureAwait(false);
            Assert.AreSame(source.Task, completed, "等待应答超时");
            return await source.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// 解析应答报文，失败直接判失败
        /// </summary>
        /// <param name="payload">报文字节</param>
        /// <returns>解析出的消息</returns>
        private static StunMessage ParseResponse(byte[] payload)
        {
            StunMessage message;
            StunParseError error;
            Assert.IsTrue(StunMessage.TryParse(payload, out message, out error), "应答解析失败：" + error);
            Assert.IsNotNull(message);
            return message;
        }

        /// <summary>
        /// 解码 XOR-MAPPED-ADDRESS，失败直接判失败
        /// </summary>
        /// <param name="response">应答消息，异或掩码依赖其事务 ID</param>
        /// <param name="attribute">地址属性</param>
        /// <returns>解码出的端点</returns>
        private static IPEndPoint DecodeMappedAddress(StunMessage response, StunAttribute attribute)
        {
            IPEndPoint endPoint;
            Assert.IsTrue(StunAddressCodec.TryDecodeAddress(attribute, response.TransactionId.ToArray(), out endPoint), "XOR-MAPPED-ADDRESS 解码失败");
            return endPoint;
        }
    }
}