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
*命名空间：SAEA.P2P.StunServer
*文件名： StunServerOptions
*版本号： v26.11.5.1
*唯一标识：df882319-1646-4461-a130-307159139591
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 19:00:00
*描述：STUN 服务端监听与响应配置
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 20:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增双栈监听开关
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

using SAEA.P2P.Stun;
using SAEA.P2P.Transport;

namespace SAEA.P2P.StunServer
{
    /// <summary>
    /// STUN 服务端配置：主端点必须给出，备用端点用于 RFC 5780 的换地址与换端口探测。
    /// 两个端点都配置时，服务端会在两个地址与两个端口的所有可用组合上监听。
    /// </summary>
    public sealed class StunServerOptions
    {
        /// <summary>
        /// SOFTWARE 属性值
        /// </summary>
        private string _software = "SAEA.P2P STUN Server";

        /// <summary>
        /// 创建配置
        /// </summary>
        /// <param name="primaryEndPoint">主监听端点；端口为 0 时由系统分配，但此时不能配置备用端点</param>
        /// <param name="alternateEndPoint">备用监听端点；为 null 时不提供 OTHER-ADDRESS，换地址换端口请求返回 420</param>
        public StunServerOptions(IPEndPoint primaryEndPoint, IPEndPoint alternateEndPoint = null)
        {
            if (primaryEndPoint == null)
            {
                throw new ArgumentNullException(nameof(primaryEndPoint));
            }

            PrimaryEndPoint = primaryEndPoint;
            AlternateEndPoint = alternateEndPoint;
        }

        /// <summary>
        /// 主监听端点
        /// </summary>
        public IPEndPoint PrimaryEndPoint { get; }

        /// <summary>
        /// 备用监听端点；为 null 时只监听主端点
        /// </summary>
        public IPEndPoint AlternateEndPoint { get; }

        /// <summary>
        /// SOFTWARE 属性值；为空时不携带该属性
        /// </summary>
        public string Software
        {
            get { return _software; }
            set { _software = value; }
        }

        /// <summary>
        /// 传输工厂；为 null 时每个监听端点创建一个 <see cref="SaeaUdpTransport"/>，测试可替换为用户态实现
        /// </summary>
        public Func<IUdpTransport> TransportFactory { get; set; }

        /// <summary>
        /// 是否使用双栈监听；默认 false。
        /// 仅当主端点为 <see cref="IPAddress.IPv6Any"/> 且未配置备用端点时生效，此时用一个双栈 UDP 套接字同时接收 IPv4 与 IPv6。
        /// </summary>
        public bool EnableDualMode { get; set; }

        /// <summary>
        /// 非 Binding 方法的分派点，供 TURN 等方法接入同一端口；返回 null 表示不处理。
        /// 参数依次为请求、客户端来源端点与收到请求的监听端点。
        /// </summary>
        public Func<StunMessage, IPEndPoint, StunServerEndpoint, byte[]> RequestHandler { get; set; }

        /// <summary>
        /// 原始数据报入口，在 STUN 解析之前调用，供 TURN 的 ChannelData 等非 STUN 报文接入同一端口；
        /// 参数依次为数据报、客户端来源端点与收到数据报的监听端点，返回 true 表示已处理、不再交给 STUN 解析。
        /// </summary>
        public Func<ReadOnlyMemory<byte>, IPEndPoint, StunServerEndpoint, bool> DatagramHandler { get; set; }

        /// <summary>
        /// 是否配置了备用端点
        /// </summary>
        internal bool HasAlternate
        {
            get { return AlternateEndPoint != null; }
        }

        /// <summary>
        /// 备用端点是否使用与主端点不同的地址
        /// </summary>
        internal bool CanChangeAddress
        {
            get { return AlternateEndPoint != null && !AlternateEndPoint.Address.Equals(PrimaryEndPoint.Address); }
        }

        /// <summary>
        /// 备用端点是否使用与主端点不同的端口
        /// </summary>
        internal bool CanChangePort
        {
            get { return AlternateEndPoint != null && AlternateEndPoint.Port != PrimaryEndPoint.Port; }
        }

        /// <summary>
        /// 创建一个尚未绑定的传输
        /// </summary>
        /// <returns>传输实例</returns>
        internal IUdpTransport CreateTransport()
        {
            var factory = TransportFactory;
            return factory != null ? factory() : new SaeaUdpTransport(EnableDualMode);
        }

        /// <summary>
        /// 计算需要监听的端点集合：两个地址与两个端口的所有组合
        /// </summary>
        /// <returns>待绑定端点</returns>
        internal IReadOnlyList<IPEndPoint> BuildBindEndPoints()
        {
            Validate();

            var addresses = new List<IPAddress> { PrimaryEndPoint.Address };
            if (AlternateEndPoint != null && !AlternateEndPoint.Address.Equals(PrimaryEndPoint.Address))
            {
                addresses.Add(AlternateEndPoint.Address);
            }

            var ports = new List<int> { PrimaryEndPoint.Port };
            if (AlternateEndPoint != null && AlternateEndPoint.Port != PrimaryEndPoint.Port)
            {
                ports.Add(AlternateEndPoint.Port);
            }

            // 同一地址与端口的组合只监听一次，避免重复绑定同一端口
            var result = new List<IPEndPoint>();
            for (var i = 0; i < addresses.Count; i++)
            {
                for (var j = 0; j < ports.Count; j++)
                {
                    result.Add(new IPEndPoint(addresses[i], ports[j]));
                }
            }
            return result;
        }

        /// <summary>
        /// 校验端点组合；启用双栈时主端点必须是 IPv6 任意地址且不能配置备用端点，
        /// 否则两个端点必须使用同一地址族，且端口不能为 0
        /// </summary>
        private void Validate()
        {
            if (EnableDualMode)
            {
                if (PrimaryEndPoint.AddressFamily != AddressFamily.InterNetworkV6
                    || !PrimaryEndPoint.Address.Equals(IPAddress.IPv6Any))
                {
                    throw new ArgumentException("启用双栈监听时主端点必须是 IPv6 任意地址（::）");
                }

                if (AlternateEndPoint != null)
                {
                    // 双栈套接字已同时覆盖两个地址族，再配备用端点会与换地址探测的语义冲突
                    throw new ArgumentException("启用双栈监听时不能配置备用端点");
                }

                return;
            }

            if (AlternateEndPoint == null)
            {
                return;
            }

            if (PrimaryEndPoint.Port == 0 || AlternateEndPoint.Port == 0)
            {
                throw new ArgumentException("配置备用端点时主端点与备用端点都必须指定非零端口");
            }

            if (PrimaryEndPoint.Address.Equals(AlternateEndPoint.Address) && PrimaryEndPoint.Port == AlternateEndPoint.Port)
            {
                throw new ArgumentException("备用端点不能与主端点完全相同");
            }

            if (PrimaryEndPoint.AddressFamily != AlternateEndPoint.AddressFamily)
            {
                throw new ArgumentException("主端点与备用端点必须使用同一地址族");
            }
        }
    }
}