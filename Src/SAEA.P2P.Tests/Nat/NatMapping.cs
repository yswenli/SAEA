/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Tests.Nat
*文件名： NatMapping
*版本号： net10.0
*唯一标识：a08237bd-7f7d-4df7-97ba-62bddd101d96
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 13:40:00
*描述：NAT 内部端点与外部端点的映射记录
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 13:40:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 NAT 映射记录
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace SAEA.P2P.Tests.Nat
{
    /// <summary>
    /// 一条 NAT 映射：内部端点经外部端点出入，并记录内部主机曾访问过的目标用于过滤判定。
    /// </summary>
    public sealed class NatMapping
    {
        /// <summary>
        /// 曾访问过的目标地址，供地址相关过滤使用
        /// </summary>
        private readonly HashSet<IPAddress> _permittedAddresses = new HashSet<IPAddress>();

        /// <summary>
        /// 曾访问过的目标端点，供地址端口相关过滤使用
        /// </summary>
        private readonly HashSet<IPEndPoint> _permittedEndPoints = new HashSet<IPEndPoint>();

        /// <summary>
        /// 内部端点
        /// </summary>
        public IPEndPoint InternalEndPoint { get; }

        /// <summary>
        /// 对外呈现的外部端点
        /// </summary>
        public IPEndPoint ExternalEndPoint { get; }

        /// <summary>
        /// 所属协议
        /// </summary>
        public ProtocolType Protocol { get; }

        /// <summary>
        /// 是否为静态端口映射
        /// </summary>
        public bool IsStatic { get; }

        /// <summary>
        /// 映射键：同一 NAT 内唯一标识这条映射
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// 最近一次活动时间，用于空闲超时
        /// </summary>
        public DateTime LastActivity { get; private set; }

        /// <summary>
        /// 承载外部端点的套接字；静态 TCP 映射为监听套接字
        /// </summary>
        internal Socket Socket { get; set; }

        /// <summary>
        /// 创建映射记录
        /// </summary>
        /// <param name="key">映射键</param>
        /// <param name="protocol">所属协议</param>
        /// <param name="internalEndPoint">内部端点</param>
        /// <param name="externalEndPoint">外部端点</param>
        /// <param name="isStatic">是否为静态映射</param>
        /// <param name="now">创建时刻</param>
        public NatMapping(string key, ProtocolType protocol, IPEndPoint internalEndPoint, IPEndPoint externalEndPoint, bool isStatic, DateTime now)
        {
            Key = key;
            Protocol = protocol;
            InternalEndPoint = internalEndPoint;
            ExternalEndPoint = externalEndPoint;
            IsStatic = isStatic;
            LastActivity = now;
        }

        /// <summary>
        /// 记录内部主机访问过的目标，建立过滤许可
        /// </summary>
        /// <param name="destination">目标端点</param>
        public void Permit(IPEndPoint destination)
        {
            if (destination == null) return;
            lock (_permittedAddresses)
            {
                _permittedAddresses.Add(destination.Address);
                _permittedEndPoints.Add(destination);
            }
        }

        /// <summary>
        /// 判断目标地址是否曾被访问
        /// </summary>
        /// <param name="address">来源地址</param>
        /// <returns>曾访问过时为 true</returns>
        public bool HasPermittedAddress(IPAddress address)
        {
            if (address == null) return false;
            lock (_permittedAddresses) return _permittedAddresses.Contains(address);
        }

        /// <summary>
        /// 判断目标端点是否曾被访问
        /// </summary>
        /// <param name="endPoint">来源端点</param>
        /// <returns>曾访问过时为 true</returns>
        public bool HasPermittedEndPoint(IPEndPoint endPoint)
        {
            if (endPoint == null) return false;
            lock (_permittedAddresses) return _permittedEndPoints.Contains(endPoint);
        }

        /// <summary>
        /// 刷新最近活动时间
        /// </summary>
        /// <param name="now">当前时刻</param>
        internal void Touch(DateTime now)
        {
            LastActivity = now;
        }

        /// <summary>
        /// 判断映射是否已空闲超时
        /// </summary>
        /// <param name="now">当前时刻</param>
        /// <param name="lifetime">空闲超时</param>
        /// <returns>已超时时为 true</returns>
        public bool IsExpired(DateTime now, TimeSpan lifetime)
        {
            return !IsStatic && now - LastActivity > lifetime;
        }

        /// <summary>
        /// 输出便于用例定位的映射描述
        /// </summary>
        /// <returns>映射描述</returns>
        public override string ToString()
        {
            return $"{Protocol} {InternalEndPoint} <-> {ExternalEndPoint}{(IsStatic ? " (static)" : string.Empty)}";
        }
    }
}