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
*文件名： TurnAuthResult
*版本号： v26.11.5.1
*唯一标识：3f863f90-3d41-44d7-be08-b30946a06c82
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 23:30:00
*描述：TURN 鉴权结果与 401/438 挑战属性
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 23:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 鉴权结果与挑战属性
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

using SAEA.P2P.Stun;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN 鉴权结果：成功时携带长期密钥，失败时携带错误码、原因与可直接写入应答的挑战属性。
    /// 长期密钥与密码都属于敏感信息，<see cref="ToString"/> 只输出错误码与原因。
    /// </summary>
    public sealed class TurnAuthResult
    {
        /// <summary>
        /// 长期密钥；失败时为空数组
        /// </summary>
        private readonly byte[] _integrityKey;

        /// <summary>
        /// 挑战属性，成功时为空列表
        /// </summary>
        private readonly IReadOnlyList<StunAttribute> _challengeAttributes;

        /// <summary>
        /// 创建鉴权结果；只由 <see cref="TurnAuthContext"/> 调用
        /// </summary>
        /// <param name="success">是否通过鉴权</param>
        /// <param name="errorCode">错误码，成功时为 0</param>
        /// <param name="reason">原因短语</param>
        /// <param name="username">用户名</param>
        /// <param name="nonce">本次有效的 nonce</param>
        /// <param name="integrityKey">长期密钥</param>
        /// <param name="challengeAttributes">挑战属性</param>
        private TurnAuthResult(bool success, int errorCode, string reason, string username, string nonce, byte[] integrityKey, IReadOnlyList<StunAttribute> challengeAttributes)
        {
            Success = success;
            ErrorCode = errorCode;
            Reason = reason;
            Username = username;
            Nonce = nonce;
            _integrityKey = integrityKey ?? new byte[0];
            _challengeAttributes = challengeAttributes ?? new StunAttribute[0];
        }

        /// <summary>
        /// 是否通过鉴权
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// 错误码，成功时为 0
        /// </summary>
        public int ErrorCode { get; private set; }

        /// <summary>
        /// 原因短语
        /// </summary>
        public string Reason { get; private set; }

        /// <summary>
        /// 用户名
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        /// 本次有效的 nonce；失败时是刚签发的新值
        /// </summary>
        public string Nonce { get; private set; }

        /// <summary>
        /// 长期密钥的副本；失败时为空数组
        /// </summary>
        public byte[] IntegrityKey
        {
            get
            {
                var copy = new byte[_integrityKey.Length];
                Buffer.BlockCopy(_integrityKey, 0, copy, 0, _integrityKey.Length);
                return copy;
            }
        }

        /// <summary>
        /// 挑战属性：ERROR-CODE 加 REALM 与 NONCE，可直接写入错误应答
        /// </summary>
        public IReadOnlyList<StunAttribute> ChallengeAttributes
        {
            get { return _challengeAttributes; }
        }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="nonce">本次有效的 nonce</param>
        /// <param name="integrityKey">长期密钥</param>
        /// <returns>成功结果</returns>
        internal static TurnAuthResult Ok(string username, string nonce, byte[] integrityKey)
        {
            return new TurnAuthResult(true, 0, null, username, nonce, integrityKey, null);
        }

        /// <summary>
        /// 创建失败结果；401 带 REALM 加 NONCE，438 带 NONCE 加 REALM，其余错误码只带 ERROR-CODE
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="reason">原因短语，为 null 时取默认值</param>
        /// <param name="realm">认证域</param>
        /// <param name="nonce">新签发的 nonce</param>
        /// <returns>失败结果</returns>
        internal static TurnAuthResult Challenge(int errorCode, string reason, string realm, string nonce)
        {
            if (string.IsNullOrEmpty(reason))
            {
                reason = TurnErrorCode.GetReason(errorCode);
            }

            var attributes = new List<StunAttribute>
            {
                TurnErrorCode.Create(errorCode, reason)
            };

            var nonceAttribute = new StunAttribute(StunAttributeType.Nonce, Encoding.UTF8.GetBytes(nonce ?? string.Empty));
            var realmAttribute = new StunAttribute(StunAttributeType.Realm, Encoding.UTF8.GetBytes(realm ?? string.Empty));

            // 438 的 NONCE 放在 REALM 之前，与设计约定的属性顺序一致
            if (errorCode == TurnErrorCode.StaleNonce)
            {
                attributes.Add(nonceAttribute);
                attributes.Add(realmAttribute);
            }
            else
            {
                attributes.Add(realmAttribute);
                attributes.Add(nonceAttribute);
            }

            return new TurnAuthResult(false, errorCode, reason, null, nonce, null, attributes);
        }

        /// <summary>
        /// 输出不含密钥与密码的摘要，供日志与诊断使用
        /// </summary>
        /// <returns>摘要文本</returns>
        public override string ToString()
        {
            if (Success)
            {
                return "TurnAuthResult(成功，用户=" + Username + ")";
            }

            return "TurnAuthResult(失败，" + ErrorCode + " " + Reason + ")";
        }
    }
}