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
*命名空间：SAEA.P2P.Tests.Turn
*文件名： TurnAuthTests
*版本号： net10.0
*唯一标识：ec186334-e915-442f-b6e2-d1332fecb2c0
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 23:50:00
*描述：TURN 两层鉴权、nonce 与临时凭据验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 23:50:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 TURN 鉴权用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Stun;
using SAEA.P2P.Turn;

namespace SAEA.P2P.Tests.Turn
{
    /// <summary>
    /// TURN 鉴权：临时凭据派生、realm 与 nonce 校验、长期密钥与 Base64 密码误用的区分。
    /// </summary>
    [TestClass]
    public class TurnAuthTests
    {
        /// <summary>
        /// 用例共享的认证域
        /// </summary>
        private const string Realm = "saea.p2p";

        /// <summary>
        /// 用例共享的共享密钥
        /// </summary>
        private const string SharedSecret = "shared-secret-for-tests";

        /// <summary>
        /// 正确凭据经长期密钥签名后应通过鉴权
        /// </summary>
        [TestMethod]
        public void TurnAuth_正确凭据_通过()
        {
            var clock = new MutableClock(DateTimeOffset.UtcNow);
            var credentials = CreateCredentialService(clock);
            var context = CreateAuthContext(credentials, clock);
            var client = new IPEndPoint(IPAddress.Loopback, 51000);

            var credential = credentials.Create("learner-1");
            var nonce = context.CreateChallenge(TurnErrorCode.Unauthorized, client).Nonce;
            var request = CreateRequest(credential, nonce, null, null);

            var result = context.Authenticate(RoundTrip(request), client);

            Assert.IsTrue(result.Success, "正确凭据应通过鉴权，实际错误码 " + result.ErrorCode);
            Assert.AreEqual(0, result.ErrorCode, "通过鉴权时不应带错误码");
            Assert.AreEqual(credential.Username, result.Username, "结果应回显用户名");
            CollectionAssert.AreEqual(
                StunIntegrity.CreateLongTermKey(credential.Username, Realm, credential.Password),
                result.IntegrityKey,
                "应返回派生出的长期密钥");
        }

        /// <summary>
        /// 用户名里的时间戳过期后，即使密码正确也必须回 401
        /// </summary>
        [TestMethod]
        public void TurnAuth_时间戳过期_返回401()
        {
            var clock = new MutableClock(DateTimeOffset.UtcNow);
            var credentials = CreateCredentialService(clock);
            var context = CreateAuthContext(credentials, clock);
            var client = new IPEndPoint(IPAddress.Loopback, 51001);

            var credential = credentials.Create("learner-2");

            // 凭据默认 600 秒有效，推进 601 秒后必须判定过期
            clock.Now = clock.Now.AddSeconds(601);
            var nonce = context.CreateChallenge(TurnErrorCode.Unauthorized, client).Nonce;
            var request = CreateRequest(credential, nonce, null, null);

            var result = context.Authenticate(RoundTrip(request), client);

            Assert.IsFalse(result.Success, "过期凭据不得通过鉴权");
            Assert.AreEqual(TurnErrorCode.Unauthorized, result.ErrorCode, "时间戳过期应回 401 而不是 438");
            Assert.IsFalse(string.IsNullOrEmpty(result.Nonce), "401 挑战必须带新 nonce");
        }

        /// <summary>
        /// 请求里的 REALM 与服务端配置不一致时必须回 401
        /// </summary>
        [TestMethod]
        public void TurnAuth_realm不匹配_返回401()
        {
            var clock = new MutableClock(DateTimeOffset.UtcNow);
            var credentials = CreateCredentialService(clock);
            var context = CreateAuthContext(credentials, clock);
            var client = new IPEndPoint(IPAddress.Loopback, 51002);

            var credential = credentials.Create("learner-3");
            var nonce = context.CreateChallenge(TurnErrorCode.Unauthorized, client).Nonce;

            // 完整性密钥按错误 realm 计算，确保失败原因是 realm 而不是 MIC
            var request = CreateRequest(credential, nonce, "other.realm", null);

            var result = context.Authenticate(RoundTrip(request), client);

            Assert.IsFalse(result.Success, "realm 不匹配不得通过鉴权");
            Assert.AreEqual(TurnErrorCode.Unauthorized, result.ErrorCode, "realm 不匹配应回 401");
            Assert.IsTrue(ContainsAttribute(result.ChallengeAttributes, StunAttributeType.Realm), "401 挑战必须带 REALM");
            Assert.IsTrue(ContainsAttribute(result.ChallengeAttributes, StunAttributeType.Nonce), "401 挑战必须带 NONCE");
        }

        /// <summary>
        /// nonce 与源 IP 和端口绑定，换一个源地址后同一 nonce 必须被拒
        /// </summary>
        [TestMethod]
        public void TurnAuth_同一nonce跨源地址_被拒()
        {
            var clock = new MutableClock(DateTimeOffset.UtcNow);
            var credentials = CreateCredentialService(clock);
            var context = CreateAuthContext(credentials, clock);
            var clientA = new IPEndPoint(IPAddress.Loopback, 51003);
            var clientB = new IPEndPoint(IPAddress.Loopback, 51004);

            var credential = credentials.Create("learner-4");
            var nonce = context.CreateChallenge(TurnErrorCode.Unauthorized, clientA).Nonce;
            var request = CreateRequest(credential, nonce, null, null);

            var result = context.Authenticate(RoundTrip(request), clientB);

            Assert.IsFalse(result.Success, "nonce 不得跨源地址复用");
            Assert.AreEqual(TurnErrorCode.Unauthorized, result.ErrorCode, "来源不符的 nonce 应回 401");
        }

        /// <summary>
        /// 直接把 Base64 密码当作 HMAC 密钥是典型实现错误，必须校验失败
        /// </summary>
        [TestMethod]
        public void TurnAuth_用Base64密码当key_校验失败()
        {
            var clock = new MutableClock(DateTimeOffset.UtcNow);
            var credentials = CreateCredentialService(clock);
            var context = CreateAuthContext(credentials, clock);
            var client = new IPEndPoint(IPAddress.Loopback, 51005);

            var credential = credentials.Create("learner-5");
            var nonce = context.CreateChallenge(TurnErrorCode.Unauthorized, client).Nonce;
            var request = CreateRequest(credential, nonce, null, Encoding.UTF8.GetBytes(credential.Password));

            var result = context.Authenticate(RoundTrip(request), client);

            Assert.IsFalse(result.Success, "Base64 密码不是长期密钥，必须校验失败");
            Assert.AreEqual(TurnErrorCode.Unauthorized, result.ErrorCode, "MESSAGE-INTEGRITY 不符应回 401");
        }

        /// <summary>
        /// 凭据与凭据服务的 ToString 都不得泄露密码与共享密钥
        /// </summary>
        [TestMethod]
        public void TurnCredential_ToString_不含密码()
        {
            var clock = new MutableClock(DateTimeOffset.UtcNow);
            var credentials = CreateCredentialService(clock);
            var credential = credentials.Create("learner-6");

            Assert.IsFalse(credential.ToString().Contains(credential.Password), "凭据摘要不得输出密码");
            Assert.IsFalse(credential.ToString().Contains(SharedSecret), "凭据摘要不得输出共享密钥");
            Assert.IsFalse(credentials.ToString().Contains(SharedSecret), "凭据服务摘要不得输出共享密钥");
        }

        /// <summary>
        /// 用给定时钟创建凭据服务
        /// </summary>
        /// <param name="clock">可注入时钟</param>
        /// <returns>凭据服务</returns>
        private static TurnCredentialService CreateCredentialService(MutableClock clock)
        {
            return new TurnCredentialService(SharedSecret, Realm, 600, TurnCredentialService.DefaultSeparator, clock.Read);
        }

        /// <summary>
        /// 用给定凭据服务与时钟创建鉴权上下文
        /// </summary>
        /// <param name="credentials">凭据服务</param>
        /// <param name="clock">可注入时钟</param>
        /// <returns>鉴权上下文</returns>
        private static TurnAuthContext CreateAuthContext(TurnCredentialService credentials, MutableClock clock)
        {
            var nonces = new TurnNonceService("nonce-secret-for-tests", 600, clock.Read);
            return new TurnAuthContext(credentials, nonces);
        }

        /// <summary>
        /// 构造带鉴权属性的 Allocate 请求
        /// </summary>
        /// <param name="credential">临时凭据</param>
        /// <param name="nonce">nonce</param>
        /// <param name="realm">REALM 覆盖值；为 null 时用凭据里的域</param>
        /// <param name="integrityKey">签名密钥覆盖值；为 null 时用长期密钥</param>
        /// <returns>请求消息</returns>
        private static StunMessage CreateRequest(TurnCredential credential, string nonce, string realm, byte[] integrityKey)
        {
            var request = TurnMessage.CreateAllocate(17);
            var actualRealm = realm ?? credential.Realm;

            var attributes = new List<StunAttribute>(request.Attributes)
            {
                new StunAttribute(StunAttributeType.Username, Encoding.UTF8.GetBytes(credential.Username)),
                new StunAttribute(StunAttributeType.Realm, Encoding.UTF8.GetBytes(actualRealm)),
                new StunAttribute(StunAttributeType.Nonce, Encoding.UTF8.GetBytes(nonce))
            };
            request.SetAttributes(attributes);

            var key = integrityKey ?? StunIntegrity.CreateLongTermKey(credential.Username, actualRealm, credential.Password);
            StunIntegrity.Append(request, key);
            return request;
        }

        /// <summary>
        /// 按报文形式往返一次，确保校验走的是真实字节而不是内存对象
        /// </summary>
        /// <param name="message">待往返的消息</param>
        /// <returns>解析后的消息</returns>
        private static StunMessage RoundTrip(StunMessage message)
        {
            StunMessage parsed;
            StunParseError error;
            Assert.IsTrue(StunMessage.TryParse(message.ToArray(), out parsed, out error), "消息应能重新解析，错误 " + error);
            return parsed;
        }

        /// <summary>
        /// 判断属性列表里是否存在指定类型
        /// </summary>
        /// <param name="attributes">属性列表</param>
        /// <param name="type">属性类型</param>
        /// <returns>存在时返回 true</returns>
        private static bool ContainsAttribute(IReadOnlyList<StunAttribute> attributes, StunAttributeType type)
        {
            for (var i = 0; i < attributes.Count; i++)
            {
                if (attributes[i].Type == (ushort)type)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 可变时钟，用于让过期判定在用例内即时生效
        /// </summary>
        private sealed class MutableClock
        {
            /// <summary>
            /// 创建时钟
            /// </summary>
            /// <param name="now">初始时间</param>
            public MutableClock(DateTimeOffset now)
            {
                Now = now;
            }

            /// <summary>
            /// 当前时间
            /// </summary>
            public DateTimeOffset Now { get; set; }

            /// <summary>
            /// 读取当前时间
            /// </summary>
            /// <returns>当前时间</returns>
            public DateTimeOffset Read()
            {
                return Now;
            }
        }
    }
}