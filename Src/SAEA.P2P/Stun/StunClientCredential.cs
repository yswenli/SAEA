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
*命名空间：SAEA.P2P.Stun
*文件名： StunClientCredential
*版本号： v26.11.5.1
*唯一标识：986e0fd5-b304-4a26-a750-36d3cacf30d6
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 17:23:00
*描述：STUN 客户端凭据
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 17:23:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 STUN 客户端凭据
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Stun
{
    /// <summary>
    /// STUN 客户端凭据：服务端挑战带认证域时按长期凭据派生密钥，否则按短凭据直接使用口令。
    /// 口令与派生密钥都不会出现在 <see cref="ToString"/> 里，避免随日志泄露。
    /// </summary>
    public sealed class StunClientCredential
    {
        /// <summary>
        /// 创建凭据；口令不得为空
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">口令</param>
        /// <param name="realm">预置认证域；为 null 时以服务端挑战里的认证域为准</param>
        public StunClientCredential(string username, string password, string realm = null)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("用户名不能为空", nameof(username));
            }
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("口令不能为空", nameof(password));
            }

            Username = username;
            Password = password;
            Realm = realm;
        }

        /// <summary>
        /// 用户名
        /// </summary>
        public string Username { get; }

        /// <summary>
        /// 口令；不参与日志输出
        /// </summary>
        public string Password { get; }

        /// <summary>
        /// 预置认证域；为 null 时只使用服务端挑战给出的认证域
        /// </summary>
        public string Realm { get; }

        /// <summary>
        /// 输出用户名与是否预置认证域，绝不包含口令
        /// </summary>
        /// <returns>不含敏感信息的描述</returns>
        public override string ToString()
        {
            return "StunClientCredential{Username=" + Username + ", Realm=" + (Realm ?? "<challenge>") + "}";
        }
    }
}