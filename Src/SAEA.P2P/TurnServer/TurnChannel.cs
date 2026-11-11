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
*文件名： TurnChannel
*版本号： v26.11.5.1
*唯一标识：a74ad698-c52b-475e-9e0a-8175c269d379
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 00:20:00
*描述：TURN 通道绑定
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 00:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 通道绑定
*
*****************************************************************************/
using System;
using System.Net;

namespace SAEA.P2P.TurnServer
{
    /// <summary>
    /// TURN 通道绑定：通道号只允许 0x4000 到 0x4FFF，生命周期 600 秒。
    /// 绑定过期后同一通道号在 300 秒内不得改绑到其它对端，规避旧报文被误认。
    /// 时间推进与并发访问由所属 allocation 统一负责。
    /// </summary>
    public sealed class TurnChannel
    {
        /// <summary>
        /// 通道生命周期，单位秒
        /// </summary>
        public const int LifetimeSeconds = 600;

        /// <summary>
        /// 通道过期后禁止改绑到其它对端的时长，单位秒
        /// </summary>
        public const int RebindQuarantineSeconds = 300;

        /// <summary>
        /// 创建通道绑定
        /// </summary>
        /// <param name="channelNumber">通道号，取值 0x4000 到 0x4FFF</param>
        /// <param name="peer">对端地址</param>
        /// <param name="now">当前时间</param>
        /// <exception cref="ArgumentOutOfRangeException">通道号不在允许区间</exception>
        /// <exception cref="ArgumentNullException">peer 为 null</exception>
        public TurnChannel(ushort channelNumber, IPEndPoint peer, DateTimeOffset now)
        {
            if (channelNumber < 0x4000 || channelNumber > 0x4FFF)
            {
                throw new ArgumentOutOfRangeException(nameof(channelNumber), "通道号必须落在 0x4000 到 0x4FFF 之间");
            }
            if (peer == null)
            {
                throw new ArgumentNullException(nameof(peer));
            }

            ChannelNumber = channelNumber;
            Peer = peer;
            ExpiresAt = now.AddSeconds(LifetimeSeconds);
        }

        /// <summary>
        /// 通道号
        /// </summary>
        public ushort ChannelNumber { get; private set; }

        /// <summary>
        /// 绑定的对端地址，含 IP 与端口
        /// </summary>
        public IPEndPoint Peer { get; private set; }

        /// <summary>
        /// 过期时间
        /// </summary>
        public DateTimeOffset ExpiresAt { get; private set; }

        /// <summary>
        /// 刷新通道到当前时间之后 600 秒
        /// </summary>
        /// <param name="now">当前时间</param>
        public void Refresh(DateTimeOffset now)
        {
            ExpiresAt = now.AddSeconds(LifetimeSeconds);
        }

        /// <summary>
        /// 判断通道是否已过期
        /// </summary>
        /// <param name="now">当前时间</param>
        /// <returns>已过期返回 true</returns>
        public bool IsExpired(DateTimeOffset now)
        {
            return now >= ExpiresAt;
        }
    }
}