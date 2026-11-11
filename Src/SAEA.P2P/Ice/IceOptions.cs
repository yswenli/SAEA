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
*命名空间：SAEA.P2P.Ice
*文件名： IceOptions
*版本号： v26.11.5.1
*唯一标识：be2c1df9-3dbe-4fec-bc83-ee7ada3fbeae
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 12:15:00
*描述：ICE 会话配置
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 12:15:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 ICE 会话配置
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;

using SAEA.P2P.Transport;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// ICE 会话配置：候选收集范围、收集预算与传输实现，各项都有可用默认值
    /// </summary>
    public sealed class IceOptions
    {
        /// <summary>
        /// 是否收集回环地址候选
        /// </summary>
        private bool _allowLoopbackCandidates;

        /// <summary>
        /// 是否收集 IPv6 地址
        /// </summary>
        private bool _enableIpv6 = true;

        /// <summary>
        /// 收集预算
        /// </summary>
        private TimeSpan _gatheringTimeout = TimeSpan.FromSeconds(3);

        /// <summary>
        /// 分量编号
        /// </summary>
        private int _component = 1;

        /// <summary>
        /// STUN 服务器端点
        /// </summary>
        private readonly List<IPEndPoint> _stunServers = new List<IPEndPoint>();

        /// <summary>
        /// 指定绑定的本地地址
        /// </summary>
        private readonly List<IPAddress> _bindAddresses = new List<IPAddress>();

        /// <summary>
        /// 传输实现工厂
        /// </summary>
        private Func<IUdpTransport> _transportFactory;

        /// <summary>
        /// 是否收集回环地址候选；只为本地自动化测试开放，生产默认关闭
        /// </summary>
        public bool AllowLoopbackCandidates
        {
            get { return _allowLoopbackCandidates; }
            set { _allowLoopbackCandidates = value; }
        }

        /// <summary>
        /// 是否收集 IPv6 地址；关闭后不收集 IPv6 地址
        /// </summary>
        public bool EnableIpv6
        {
            get { return _enableIpv6; }
            set { _enableIpv6 = value; }
        }

        /// <summary>
        /// 收集预算；到时先返回已有候选，不再等待未完成的收集
        /// </summary>
        public TimeSpan GatheringTimeout
        {
            get { return _gatheringTimeout; }
            set { _gatheringTimeout = value; }
        }

        /// <summary>
        /// 分量编号，默认 1
        /// </summary>
        public int Component
        {
            get { return _component; }
            set { _component = value; }
        }

        /// <summary>
        /// STUN 服务器端点；按地址族匹配，多个服务器取第一个成功
        /// </summary>
        public IList<IPEndPoint> StunServers
        {
            get { return _stunServers; }
        }

        /// <summary>
        /// 指定绑定的本地地址；为空时按网卡枚举，非空时只用这些地址绑定（测试虚拟主机用），仍需通过 loopback 与 v4-mapped 过滤
        /// </summary>
        public IList<IPAddress> BindAddresses
        {
            get { return _bindAddresses; }
        }

        /// <summary>
        /// 传输实现工厂；为空时用 SaeaUdpTransport，测试可替换为经用户态 NAT 代理的实现
        /// </summary>
        public Func<IUdpTransport> TransportFactory
        {
            get { return _transportFactory; }
            set { _transportFactory = value; }
        }

        /// <summary>
        /// 按会话复制配置，避免共享列表被并发修改
        /// </summary>
        /// <returns>配置副本；标量属性浅拷贝，列表逐项复制，传输工厂直接引用</returns>
        public IceOptions Clone()
        {
            var clone = new IceOptions();
            clone._allowLoopbackCandidates = _allowLoopbackCandidates;
            clone._enableIpv6 = _enableIpv6;
            clone._gatheringTimeout = _gatheringTimeout;
            clone._component = _component;
            clone._transportFactory = _transportFactory;

            for (int i = 0; i < _stunServers.Count; i++)
            {
                clone._stunServers.Add(_stunServers[i]);
            }
            for (int i = 0; i < _bindAddresses.Count; i++)
            {
                clone._bindAddresses.Add(_bindAddresses[i]);
            }

            return clone;
        }
    }
}