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
*文件名： AuthChallenge
*版本号： v26.4.23.1
*唯一标识：c5a64e50-3177-4b5c-9879-4aa9efa4f191
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:04:05
*描述：AuthChallenge类
*
*=====================================================================
*修改标记
*修改时间：2026/04/20 16:04:05
*修改人： yswenli
*版本号： v26.4.23.1
*描述：AuthChallenge类
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Security
{
    public class AuthChallenge
    {
        public string ChallengeId { get; set; }
        public string ChallengeData { get; set; }
        public DateTime CreatedTime { get; set; }
        public int TimeoutMs { get; set; } = 30000;

        public static AuthChallenge Create()
        {
            return new AuthChallenge
            {
                ChallengeId = Guid.NewGuid().ToString("N"),
                ChallengeData = Guid.NewGuid().ToString("N"),
                CreatedTime = DateTime.UtcNow
            };
        }

        public bool IsExpired => (DateTime.UtcNow - CreatedTime).TotalMilliseconds > TimeoutMs;
    }
}