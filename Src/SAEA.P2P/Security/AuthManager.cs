/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.P2P.Security
*文件名： AuthManager
*版本号： v26.4.23.1
*唯一标识：f0acc53f-1265-465c-ab21-1bd02b72e6b7
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:04:05
*描述：AuthManager管理类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:20:16
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Security
{
    /// <summary>
    /// 基于节点口令的挑战响应鉴权：双方用相同口令对挑战数据做 SHA-256 后比对结果
    /// </summary>
    public class AuthManager
    {
        private string _nodeIdPassword;
        private CryptoService _cryptoService;

        /// <summary>
        /// 使用节点口令初始化鉴权管理器
        /// </summary>
        /// <param name="nodeIdPassword">节点口令，参与响应哈希，为 null 时按空串参与拼接</param>
        /// <param name="cryptoService">预留的加解密服务，当前鉴权流程未使用，可为 null</param>
        public AuthManager(string nodeIdPassword, CryptoService cryptoService = null)
        {
            _nodeIdPassword = nodeIdPassword;
            _cryptoService = cryptoService;
        }

        /// <summary>
        /// 按挑战数据加节点口令的 SHA-256 摘要计算响应
        /// </summary>
        /// <param name="challenge">待应答的挑战，为 null 或已过期时抛出 P2PException</param>
        /// <returns>64 位小写十六进制摘要</returns>
        public string ComputeResponse(AuthChallenge challenge)
        {
            if (challenge == null || challenge.IsExpired)
                throw new Common.P2PException(Common.ErrorCode.AuthFailed, "Challenge expired or null");

            var input = challenge.ChallengeData + _nodeIdPassword;
            return ComputeHash(input);
        }

        /// <summary>
        /// 比对对端响应与本地计算的期望摘要
        /// </summary>
        /// <param name="challenge">待应答的挑战，为 null 或已过期时抛出 P2PException</param>
        /// <param name="response">对端返回的响应摘要，区分大小写且不做空值特判</param>
        /// <returns>与期望摘要完全一致时为 true</returns>
        public bool VerifyResponse(AuthChallenge challenge, string response)
        {
            var expected = ComputeResponse(challenge);
            return expected == response;
        }

        private string ComputeHash(string input)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(input);
                var hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
    }
}