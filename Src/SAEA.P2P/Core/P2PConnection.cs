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
*命名空间：SAEA.P2P.Core
*文件名： P2PConnection
*版本号： v26.11.5.1
*唯一标识：335c07cd-e3f6-4aac-8b16-425c0e653521
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 10:21:07
*描述：节点间连接描述
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 10:21:07
*修改人： yswenli
*版本号： v26.11.5.1
*描述：节点间连接描述
*
*****************************************************************************/
using System;
using System.Net;

using SAEA.P2P.Channel;

namespace SAEA.P2P.Core
{
    /// <summary>
    /// 两个节点之间已建立的连接描述，通道类型与两端端点都取自选中结果
    /// </summary>
    public sealed class P2PConnection
    {
        /// <summary>
        /// 创建连接描述
        /// </summary>
        /// <param name="peerId">对端节点标识，不可为空</param>
        /// <param name="channel">选路结果写入的通道类型</param>
        /// <param name="localEndPoint">本端端点，局域网发现模式可能尚未确定时为 null</param>
        /// <param name="remoteEndPoint">对端端点，局域网发现模式可能尚未确定时为 null</param>
        public P2PConnection(string peerId, ChannelType channel, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint)
        {
            if (string.IsNullOrWhiteSpace(peerId))
            {
                throw new ArgumentNullException(nameof(peerId));
            }

            PeerId = peerId;
            Channel = channel;
            LocalEndPoint = localEndPoint;
            RemoteEndPoint = remoteEndPoint;
        }

        /// <summary>
        /// 对端节点标识
        /// </summary>
        public string PeerId { get; }

        /// <summary>
        /// 本次连接实际使用的通道类型
        /// </summary>
        public ChannelType Channel { get; }

        /// <summary>
        /// 本端端点，局域网发现模式可能尚未确定时为 null
        /// </summary>
        public IPEndPoint LocalEndPoint { get; }

        /// <summary>
        /// 对端端点，局域网发现模式可能尚未确定时为 null
        /// </summary>
        public IPEndPoint RemoteEndPoint { get; }
    }
}