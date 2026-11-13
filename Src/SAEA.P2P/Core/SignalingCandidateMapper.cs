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
*文件名： SignalingCandidateMapper
*版本号： v26.11.5.1
*唯一标识：55796cfb-434e-4ecd-907c-6cd47dd3e354
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 10:56:15
*描述：信令候选与 ICE 候选互转
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 10:56:15
*修改人： yswenli
*版本号： v26.11.5.1
*描述：信令候选与 ICE 候选互转
*
*****************************************************************************/
using System;
using System.Net;
using SAEA.P2P.Ice;
using SAEA.P2P.Signaling;

namespace SAEA.P2P.Core
{
    /// <summary>
    /// 信令候选与 ICE 候选互转：出站把 ICE 候选拆成文本字段，入站校验文本后重建 ICE 候选
    /// </summary>
    internal static class SignalingCandidateMapper
    {
        /// <summary>
        /// 把 ICE 候选转成信令报文候选
        /// </summary>
        /// <param name="candidate">ICE 候选，非空</param>
        /// <returns>填充完文本字段的信令候选</returns>
        /// <exception cref="ArgumentNullException">候选为空</exception>
        /// <exception cref="ArgumentOutOfRangeException">候选类型或 TCP 方向类型不是已知取值</exception>
        internal static SignalingCandidate ToSignaling(Candidate candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            var result = new SignalingCandidate();
            result.Foundation = candidate.Foundation;
            result.Component = candidate.Component;
            result.Transport = candidate.Transport == IceTransportType.Tcp ? "tcp" : "udp";
            result.Priority = candidate.Priority;
            result.Address = candidate.Address.ToString();
            result.Port = candidate.Port;
            result.RelatedAddress = candidate.RelatedAddress == null ? null : candidate.RelatedAddress.ToString();
            result.RelatedPort = candidate.RelatedPort;

            switch (candidate.Type)
            {
                case CandidateType.Host:
                    result.Type = "host";
                    break;
                case CandidateType.ServerReflexive:
                    result.Type = "srflx";
                    break;
                case CandidateType.PeerReflexive:
                    result.Type = "prflx";
                    break;
                case CandidateType.Relay:
                    result.Type = "relay";
                    break;
                default:
                    // 枚举被强转成未定义值时宁可失败，也不要往信令里写一个对端读不懂的类型
                    throw new ArgumentOutOfRangeException(nameof(candidate));
            }

            switch (candidate.TcpType)
            {
                case TcpCandidateType.None:
                    // 非 TCP 候选留空，NullValueHandling.Ignore 下该字段不出现在 JSON 里
                    result.TcpType = null;
                    break;
                case TcpCandidateType.Active:
                    result.TcpType = "active";
                    break;
                case TcpCandidateType.Passive:
                    result.TcpType = "passive";
                    break;
                case TcpCandidateType.SimultaneousOpen:
                    result.TcpType = "so";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(candidate));
            }

            return result;
        }

        /// <summary>
        /// 尝试把信令报文候选还原成 ICE 候选，任何字段非法都返回 false 而不抛异常
        /// </summary>
        /// <param name="source">信令报文候选，允许为空</param>
        /// <param name="candidate">还原出的 ICE 候选，失败时为 null</param>
        /// <returns>字段全部合法并成功构造返回 true</returns>
        internal static bool TryToCandidate(SignalingCandidate source, out Candidate candidate)
        {
            candidate = null;
            if (source == null)
            {
                return false;
            }

            IPAddress address;
            if (!IPAddress.TryParse(source.Address, out address))
            {
                return false;
            }

            if (source.Port < 1 || source.Port > 65535)
            {
                return false;
            }

            // 分量缺省按第一分量处理，超出候选类允许范围的一律丢弃
            int component = source.Component <= 0 ? 1 : source.Component;
            if (component > 256)
            {
                return false;
            }

            IceTransportType transport;
            if (string.Equals(source.Transport, "udp", StringComparison.OrdinalIgnoreCase))
            {
                transport = IceTransportType.Udp;
            }
            else if (string.Equals(source.Transport, "tcp", StringComparison.OrdinalIgnoreCase))
            {
                transport = IceTransportType.Tcp;
            }
            else
            {
                return false;
            }

            CandidateType type;
            if (string.Equals(source.Type, "host", StringComparison.OrdinalIgnoreCase))
            {
                type = CandidateType.Host;
            }
            else if (string.Equals(source.Type, "srflx", StringComparison.OrdinalIgnoreCase))
            {
                type = CandidateType.ServerReflexive;
            }
            else if (string.Equals(source.Type, "prflx", StringComparison.OrdinalIgnoreCase))
            {
                type = CandidateType.PeerReflexive;
            }
            else if (string.Equals(source.Type, "relay", StringComparison.OrdinalIgnoreCase))
            {
                type = CandidateType.Relay;
            }
            else
            {
                return false;
            }

            TcpCandidateType tcpType;
            if (string.IsNullOrWhiteSpace(source.TcpType))
            {
                tcpType = TcpCandidateType.None;
            }
            else if (string.Equals(source.TcpType, "active", StringComparison.OrdinalIgnoreCase))
            {
                tcpType = TcpCandidateType.Active;
            }
            else if (string.Equals(source.TcpType, "passive", StringComparison.OrdinalIgnoreCase))
            {
                tcpType = TcpCandidateType.Passive;
            }
            else if (string.Equals(source.TcpType, "so", StringComparison.OrdinalIgnoreCase))
            {
                tcpType = TcpCandidateType.SimultaneousOpen;
            }
            else
            {
                return false;
            }

            IPAddress relatedAddress = null;
            if (!string.IsNullOrWhiteSpace(source.RelatedAddress) && !IPAddress.TryParse(source.RelatedAddress, out relatedAddress))
            {
                return false;
            }

            if (transport == IceTransportType.Tcp)
            {
                // TCP 候选必须有方向类型，候选类的 TCP 构造不接受 None
                if (tcpType == TcpCandidateType.None)
                {
                    return false;
                }
                candidate = new Candidate(type, IceTransportType.Tcp, component, address, source.Port, tcpType, relatedAddress, source.RelatedPort);
                return true;
            }

            // 信令带了优先级就沿用，避免重算后与对端记录不一致；没带则交给 ICE 按候选类型自动算
            if (source.Priority > 0)
            {
                candidate = new Candidate(type, IceTransportType.Udp, component, address, source.Port, source.Priority);
            }
            else
            {
                candidate = new Candidate(type, IceTransportType.Udp, component, address, source.Port, TcpCandidateType.None, relatedAddress, source.RelatedPort);
            }
            return true;
        }
    }
}
