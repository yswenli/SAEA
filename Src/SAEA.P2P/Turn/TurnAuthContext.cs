/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Turn
*文件名： TurnAuthContext
*版本号： v26.11.5.1
*唯一标识：628af3e7-9bca-4dd8-a7a3-ecb47fb18cfb
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 23:30:00
*描述：TURN 服务端两层鉴权与挑战生成
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 23:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 服务端两层鉴权与挑战生成
*
*****************************************************************************/
using System;
using System.Net;
using System.Text;

using SAEA.P2P.Stun;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN 服务端鉴权上下文：校验顺序为时间戳未过期、共享密钥派生密码、realm 匹配、nonce 有效，
    /// 最后用长期密钥 MD5(用户名:域:密码) 校验 MESSAGE-INTEGRITY。
    /// 直接把 Base64 密码当作 HMAC 密钥会校验失败并回 401。
    /// </summary>
    public sealed class TurnAuthContext
    {
        /// <summary>
        /// 临时凭据服务，负责第一层校验与密码派生
        /// </summary>
        private readonly TurnCredentialService _credentials;

        /// <summary>
        /// nonce 服务，负责签发与校验
        /// </summary>
        private readonly TurnNonceService _nonces;

        /// <summary>
        /// 创建鉴权上下文
        /// </summary>
        /// <param name="credentials">临时凭据服务</param>
        /// <param name="nonces">nonce 服务</param>
        /// <exception cref="ArgumentNullException">任一参数为 null</exception>
        public TurnAuthContext(TurnCredentialService credentials, TurnNonceService nonces)
        {
            if (credentials == null)
            {
                throw new ArgumentNullException(nameof(credentials));
            }
            if (nonces == null)
            {
                throw new ArgumentNullException(nameof(nonces));
            }

            _credentials = credentials;
            _nonces = nonces;
        }

        /// <summary>
        /// 认证域
        /// </summary>
        public string Realm
        {
            get { return _credentials.Realm; }
        }

        /// <summary>
        /// 认证一条请求；失败时返回可直接写入错误应答的挑战属性
        /// </summary>
        /// <param name="request">请求消息</param>
        /// <param name="remoteEndPoint">客户端源地址，用于绑定 nonce</param>
        /// <returns>鉴权结果</returns>
        /// <exception cref="ArgumentNullException">request 或 remoteEndPoint 为 null</exception>
        public TurnAuthResult Authenticate(StunMessage request, IPEndPoint remoteEndPoint)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            string username;
            string realm;
            string nonce;

            // 缺少任一鉴权属性都按未携带有效凭据处理
            if (!TryReadString(request, StunAttributeType.Username, out username)
                || !TryReadString(request, StunAttributeType.Realm, out realm)
                || !TryReadString(request, StunAttributeType.Nonce, out nonce))
            {
                return CreateChallenge(TurnErrorCode.Unauthorized, remoteEndPoint);
            }

            StunAttribute integrity;
            if (!request.TryGet(StunAttributeType.MessageIntegrity, out integrity))
            {
                return CreateChallenge(TurnErrorCode.Unauthorized, remoteEndPoint);
            }

            long expiresAtUnixSeconds;
            string userId;
            if (!_credentials.TryValidate(username, null, out expiresAtUnixSeconds, out userId))
            {
                // 时间戳过期、用户名格式错误都回 401，由客户端重新取凭据
                return CreateChallenge(TurnErrorCode.Unauthorized, remoteEndPoint);
            }

            if (!string.Equals(realm, _credentials.Realm, StringComparison.Ordinal))
            {
                return CreateChallenge(TurnErrorCode.Unauthorized, remoteEndPoint);
            }

            bool expired;
            if (!_nonces.TryValidate(nonce, remoteEndPoint, out expired))
            {
                return CreateChallenge(expired ? TurnErrorCode.StaleNonce : TurnErrorCode.Unauthorized, remoteEndPoint);
            }

            var password = _credentials.CreatePassword(username);
            var key = StunIntegrity.CreateLongTermKey(username, realm, password);
            if (!StunIntegrity.TryVerify(request, key))
            {
                return CreateChallenge(TurnErrorCode.Unauthorized, remoteEndPoint);
            }

            return TurnAuthResult.Ok(username, nonce, key);
        }

        /// <summary>
        /// 为指定源地址生成挑战结果，供不具备鉴权的方法直接拒绝时使用
        /// </summary>
        /// <param name="errorCode">错误码，通常为 401 或 438</param>
        /// <param name="remoteEndPoint">客户端源地址</param>
        /// <returns>失败结果，NONCE 为刚签发的新值</returns>
        /// <exception cref="ArgumentNullException">remoteEndPoint 为 null</exception>
        public TurnAuthResult CreateChallenge(int errorCode, IPEndPoint remoteEndPoint)
        {
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            var nonce = _nonces.Create(remoteEndPoint);
            return TurnAuthResult.Challenge(errorCode, null, _credentials.Realm, nonce);
        }

        /// <summary>
        /// 读取指定类型的字符串属性
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="type">属性类型</param>
        /// <param name="value">UTF-8 解码后的值</param>
        /// <returns>存在时返回 true</returns>
        private static bool TryReadString(StunMessage message, StunAttributeType type, out string value)
        {
            value = null;
            StunAttribute attribute;
            if (!message.TryGet(type, out attribute))
            {
                return false;
            }

            value = Encoding.UTF8.GetString(attribute.Value.ToArray());
            return true;
        }
    }
}