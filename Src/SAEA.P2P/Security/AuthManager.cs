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
*唯一标识：ae668341-a6d0-4b37-80d8-cff7813f52a2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/4/20 16:04:05
*描述：认证管理类，处理节点认证挑战和响应验证
*
*=====================================================================
*修改标记
*修改时间：2026/4/20 16:04:05
*修改人： yswenli
*版本号： v26.4.23.1
*描述：认证管理类，处理节点认证挑战和响应验证
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Security
{
    public class AuthManager
    {
        private string _nodeIdPassword;
        private CryptoService _cryptoService;

        public AuthManager(string nodeIdPassword, CryptoService cryptoService = null)
        {
            _nodeIdPassword = nodeIdPassword;
            _cryptoService = cryptoService;
        }

        public string ComputeResponse(AuthChallenge challenge)
        {
            if (challenge == null || challenge.IsExpired)
                throw new Common.P2PException(Common.ErrorCode.AuthFailed, "Challenge expired or null");

            var input = challenge.ChallengeData + _nodeIdPassword;
            return ComputeHash(input);
        }

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