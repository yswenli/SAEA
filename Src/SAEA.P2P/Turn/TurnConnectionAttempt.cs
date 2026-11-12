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
*文件名： TurnConnectionAttempt
*版本号： v26.11.5.1
*唯一标识：7e93138a-89d8-4a75-8b34-1d78d7e3fd34
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 14:05:00
*描述：TURN over TCP 的 ConnectionAttempt 指示内容
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 14:05:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 ConnectionAttempt 指示内容
*
*****************************************************************************/
using System;
using System.Net;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// 一次 ConnectionAttempt 指示的内容：服务端分配的连接号与主动连入中继端口的对端地址。
    /// 实例不可变，可在事件回调返回后继续持有。
    /// </summary>
    public sealed class TurnConnectionAttempt
    {
        /// <summary>
        /// 创建连接尝试
        /// </summary>
        /// <param name="connectionId">服务端分配的连接号</param>
        /// <param name="peerEndPoint">主动连入的对端地址</param>
        /// <exception cref="ArgumentNullException">peerEndPoint 为 null</exception>
        public TurnConnectionAttempt(uint connectionId, IPEndPoint peerEndPoint)
        {
            if (peerEndPoint == null)
            {
                throw new ArgumentNullException(nameof(peerEndPoint), "对端地址不能为 null");
            }

            ConnectionId = connectionId;
            PeerEndPoint = peerEndPoint;
        }

        /// <summary>
        /// 服务端分配的连接号，用于另开数据连接完成 ConnectionBind
        /// </summary>
        public uint ConnectionId { get; private set; }

        /// <summary>
        /// 主动连入中继端口的对端地址
        /// </summary>
        public IPEndPoint PeerEndPoint { get; private set; }
    }
}