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
*命名空间：SAEA.P2P.TurnServer
*文件名： TurnPermission
*版本号： v26.11.5.1
*唯一标识：868e0405-2031-444f-9bfb-c934431ece28
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 00:20:00
*描述：TURN allocation 的对端权限
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 00:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 对端权限
*
*****************************************************************************/
using System;
using System.Net;

namespace SAEA.P2P.TurnServer
{
    /// <summary>
    /// TURN 权限：只按 IP 标识，端口不参与；生命周期固定 300 秒，不可配置。
    /// 时间推进与并发访问由所属 allocation 统一负责。
    /// </summary>
    public sealed class TurnPermission
    {
        /// <summary>
        /// 权限生命周期，单位秒；RFC 8656 规定固定值，不随配置变化
        /// </summary>
        public const int LifetimeSeconds = 300;

        /// <summary>
        /// 创建权限
        /// </summary>
        /// <param name="address">对端 IP</param>
        /// <param name="now">当前时间</param>
        /// <exception cref="ArgumentNullException">address 为 null</exception>
        public TurnPermission(IPAddress address, DateTimeOffset now)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            Address = address;
            ExpiresAt = now.AddSeconds(LifetimeSeconds);
        }

        /// <summary>
        /// 对端 IP
        /// </summary>
        public IPAddress Address { get; private set; }

        /// <summary>
        /// 过期时间
        /// </summary>
        public DateTimeOffset ExpiresAt { get; private set; }

        /// <summary>
        /// 刷新权限到当前时间之后 300 秒
        /// </summary>
        /// <param name="now">当前时间</param>
        public void Refresh(DateTimeOffset now)
        {
            ExpiresAt = now.AddSeconds(LifetimeSeconds);
        }

        /// <summary>
        /// 判断权限是否已过期
        /// </summary>
        /// <param name="now">当前时间</param>
        /// <returns>已过期返回 true</returns>
        public bool IsExpired(DateTimeOffset now)
        {
            return now >= ExpiresAt;
        }
    }
}