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
*文件名： Candidate
*版本号： v26.11.5.1
*唯一标识：4619e13f-e94e-41d8-8dcf-88167bbd4a7a
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 11:00:00
*描述：ICE 候选定义
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 11:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 ICE 候选定义
*
*****************************************************************************/
using System;
using System.Net;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// ICE 候选：类型、传输协议、分量、地址端口与优先级
    /// </summary>
    public sealed class Candidate
    {
        /// <summary>
        /// 按已确定的优先级创建候选，供对端反射等优先级已明确的场景使用
        /// </summary>
        /// <param name="type">候选类型</param>
        /// <param name="transport">传输协议</param>
        /// <param name="component">分量编号，取值 1 到 256</param>
        /// <param name="address">候选地址</param>
        /// <param name="port">候选端口，取值 0 到 65535</param>
        /// <param name="priority">候选优先级，由调用方按自身规则计算</param>
        /// <exception cref="ArgumentNullException">候选地址为空</exception>
        /// <exception cref="ArgumentOutOfRangeException">端口或分量编号越界</exception>
        public Candidate(CandidateType type, IceTransportType transport, int component, IPAddress address, int port, uint priority)
        {
            ValidateArguments(address, port, component);

            Type = type;
            Transport = transport;
            Component = component;
            Address = address;
            Port = port;
            Priority = priority;
            TcpType = TcpCandidateType.None;
            RelatedAddress = null;
            RelatedPort = 0;
            EndPoint = new IPEndPoint(address, port);
            Foundation = BuildFoundation(type, address, transport, null);
        }

        /// <summary>
        /// 按类型与 TCP 方向偏好自动计算优先级后创建候选
        /// </summary>
        /// <param name="type">候选类型</param>
        /// <param name="transport">传输协议</param>
        /// <param name="component">分量编号，取值 1 到 256</param>
        /// <param name="address">候选地址</param>
        /// <param name="port">候选端口，取值 0 到 65535</param>
        /// <param name="tcpType">TCP 方向类型，UDP 候选传 None</param>
        /// <param name="relatedAddress">关联地址，srflx 与 relay 候选填入</param>
        /// <param name="relatedPort">关联端口，无关联时为 0</param>
        /// <exception cref="ArgumentNullException">候选地址为空</exception>
        /// <exception cref="ArgumentOutOfRangeException">端口或分量编号越界</exception>
        /// <exception cref="ArgumentException">TCP 候选的方向类型为 None</exception>
        public Candidate(CandidateType type, IceTransportType transport, int component, IPAddress address, int port, TcpCandidateType tcpType, IPAddress relatedAddress, int relatedPort)
        {
            ValidateArguments(address, port, component);

            uint priority;
            if (transport == IceTransportType.Tcp)
            {
                priority = IcePriority.ComputeForTcp(type, component, tcpType);
            }
            else
            {
                priority = IcePriority.ComputeForUdp(type, component);
            }

            Type = type;
            Transport = transport;
            Component = component;
            Address = address;
            Port = port;
            Priority = priority;
            TcpType = tcpType;
            RelatedAddress = relatedAddress;
            RelatedPort = relatedPort;
            EndPoint = new IPEndPoint(address, port);
            Foundation = BuildFoundation(type, address, transport, relatedAddress);
        }

        /// <summary>
        /// 候选类型
        /// </summary>
        public CandidateType Type { get; }

        /// <summary>
        /// 传输协议
        /// </summary>
        public IceTransportType Transport { get; }

        /// <summary>
        /// 分量编号
        /// </summary>
        public int Component { get; }

        /// <summary>
        /// 候选地址
        /// </summary>
        public IPAddress Address { get; }

        /// <summary>
        /// 候选端口
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// 候选优先级：自动构造函数按类型与方向偏好算出，或由调用方显式指定
        /// </summary>
        public uint Priority { get; }

        /// <summary>
        /// TCP 方向类型，UDP 候选为 None
        /// </summary>
        public TcpCandidateType TcpType { get; }

        /// <summary>
        /// 关联地址：srflx 为映射前的本地地址，relay 为中继前的本地地址，host 候选为空
        /// </summary>
        public IPAddress RelatedAddress { get; }

        /// <summary>
        /// 关联端口，无关联时为 0
        /// </summary>
        public int RelatedPort { get; }

        /// <summary>
        /// 候选基础标识，由类型优先级、base 地址与传输协议构成
        /// </summary>
        public string Foundation { get; }

        /// <summary>
        /// 候选端点
        /// </summary>
        public IPEndPoint EndPoint { get; }

        /// <summary>
        /// 判断给定端点是否与本候选的地址与端口都相同
        /// </summary>
        /// <param name="endPoint">待比较的端点，为空时返回 false</param>
        /// <returns>地址与端口都相同返回 true</returns>
        public bool HasSameAddress(IPEndPoint endPoint)
        {
            if (endPoint == null)
            {
                return false;
            }
            return endPoint.Port == Port && Address.Equals(endPoint.Address);
        }

        /// <summary>
        /// 输出候选的类型、地址端口与优先级
        /// </summary>
        /// <returns>候选的文本描述</returns>
        public override string ToString()
        {
            return string.Format("{0} {1}:{2} priority={3} component={4} {5}", Type, Address, Port, Priority, Component, Transport);
        }

        private static string BuildFoundation(CandidateType type, IPAddress address, IceTransportType transport, IPAddress relatedAddress)
        {
            // RFC 8445 §5.1.1.3：foundation 不含端口，同一 base 上的不同端口必须得到相同 foundation；
            // srflx 与 relay 的 base 是关联地址，无关联地址时退回候选自身地址
            IPAddress baseAddress = relatedAddress != null ? relatedAddress : address;
            return string.Format("{0}|{1}|{2}", IcePriority.GetTypePreference(type), baseAddress, transport.ToString().ToLowerInvariant());
        }

        private static void ValidateArguments(IPAddress address, int port, int component)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }
            if (port < 0 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port), "端口必须在 0 到 65535 之间");
            }
            if (component < 1 || component > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(component), "分量编号必须在 1 到 256 之间");
            }
        }
    }
}
