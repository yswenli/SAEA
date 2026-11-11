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
*文件名： TurnCredential
*版本号： v26.11.5.1
*唯一标识：755b0d95-1cff-4554-8a16-dcafd6d6abc8
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 23:30:00
*描述：TURN 临时凭据
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 23:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 临时凭据
*
*****************************************************************************/
using System;
using System.Globalization;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN 临时凭据：用户名由过期时间戳与用户标识拼成，密码由共享密钥派生。
    /// 密码与用户名都属于敏感信息，<see cref="ToString"/> 不输出密码。
    /// </summary>
    public sealed class TurnCredential
    {
        /// <summary>
        /// 创建凭据；只由 <see cref="TurnCredentialService"/> 调用
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">Base64 形式的派生密码</param>
        /// <param name="realm">认证域</param>
        /// <param name="userId">用户标识</param>
        /// <param name="expiresAt">过期时间，UTC</param>
        /// <param name="expiresAtUnixSeconds">过期时间的 Unix 秒数</param>
        internal TurnCredential(string username, string password, string realm, string userId, DateTimeOffset expiresAt, long expiresAtUnixSeconds)
        {
            Username = username;
            Password = password;
            Realm = realm;
            UserId = userId;
            ExpiresAt = expiresAt;
            ExpiresAtUnixSeconds = expiresAtUnixSeconds;
        }

        /// <summary>
        /// 用户名，格式为「过期 Unix 秒数 + 分隔符 + 用户标识」
        /// </summary>
        public string Username { get; }

        /// <summary>
        /// Base64 形式的派生密码；用于向客户端下发，不得写入日志
        /// </summary>
        public string Password { get; }

        /// <summary>
        /// 认证域
        /// </summary>
        public string Realm { get; }

        /// <summary>
        /// 用户标识
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// 过期时间，UTC
        /// </summary>
        public DateTimeOffset ExpiresAt { get; }

        /// <summary>
        /// 过期时间的 Unix 秒数
        /// </summary>
        public long ExpiresAtUnixSeconds { get; }

        /// <summary>
        /// 输出不含密码的摘要，供日志与诊断使用
        /// </summary>
        /// <returns>摘要文本</returns>
        public override string ToString()
        {
            return "TurnCredential(用户=" + UserId + "，域=" + Realm + "，过期=" + ExpiresAt.ToString("O", CultureInfo.InvariantCulture) + ")";
        }
    }
}