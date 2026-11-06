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
    /// 一次性鉴权挑战：服务端生成随机数据，客户端据此结合节点口令计算响应
    /// </summary>
    public class AuthChallenge
    {
        /// <summary>
        /// 挑战唯一标识
        /// </summary>
        public string ChallengeId { get; set; }
        /// <summary>
        /// 挑战随机数据，参与响应哈希计算
        /// </summary>
        public string ChallengeData { get; set; }
        /// <summary>
        /// 挑战创建时间，UTC，未赋值的实例为 DateTime.MinValue
        /// </summary>
        public DateTime CreatedTime { get; set; }
        /// <summary>
        /// 挑战有效期，单位毫秒，默认 30000
        /// </summary>
        public int TimeoutMs { get; set; } = 30000;

        /// <summary>
        /// 生成新的挑战，标识与随机数据均为 32 位十六进制串
        /// </summary>
        /// <returns>创建时间为当前 UTC 时间、有效期为默认值的挑战</returns>
        public static AuthChallenge Create()
        {
            return new AuthChallenge
            {
                ChallengeId = Guid.NewGuid().ToString("N"),
                ChallengeData = Guid.NewGuid().ToString("N"),
                CreatedTime = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 挑战是否已超过有效期；CreatedTime 未赋值时按 MinValue 计算，结果恒为 true
        /// </summary>
        public bool IsExpired => (DateTime.UtcNow - CreatedTime).TotalMilliseconds > TimeoutMs;
    }
}