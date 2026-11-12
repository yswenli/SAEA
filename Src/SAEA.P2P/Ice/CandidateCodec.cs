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
*文件名： CandidateCodec
*版本号： v26.11.5.1
*唯一标识：b47ca2fc-d938-4ac4-9d7f-6904a0088dc8
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 12:00:19
*描述：ICE 候选属性行格式化
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 12:00:19
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 ICE 候选属性行格式化
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// 候选编解码：按 RFC 8839 的属性行格式输出候选，供信令交换使用
    /// </summary>
    public static class CandidateCodec
    {
        /// <summary>
        /// TCP 主动候选在候选串里固定填写的端口号
        /// </summary>
        private const int ActivePlaceholderPort = 9;

        /// <summary>
        /// 按 RFC 8839 §5.1 输出候选属性行
        /// </summary>
        /// <param name="candidate">待格式化的候选，非空</param>
        /// <returns>形如 a=candidate:... 的属性行；关联地址、TCP 方向按需附加</returns>
        /// <exception cref="ArgumentNullException">候选为空</exception>
        public static string Format(Candidate candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            var builder = new StringBuilder(128);
            builder.Append("a=candidate:");
            builder.Append(candidate.Foundation);
            builder.Append(' ');
            builder.Append(candidate.Component);
            builder.Append(' ');
            builder.Append(GetTransportName(candidate.Transport));
            builder.Append(' ');
            builder.Append(candidate.Priority);
            builder.Append(' ');
            builder.Append(FormatAddress(candidate.Address));
            builder.Append(' ');
            builder.Append(GetPort(candidate));
            builder.Append(" typ ");
            builder.Append(GetTypeName(candidate.Type));

            if (candidate.RelatedAddress != null)
            {
                builder.Append(" raddr ");
                builder.Append(FormatAddress(candidate.RelatedAddress));
                builder.Append(" rport ");
                builder.Append(candidate.RelatedPort);
            }

            if (candidate.Transport == IceTransportType.Tcp && candidate.TcpType != TcpCandidateType.None)
            {
                builder.Append(" tcptype ");
                builder.Append(GetTcpTypeName(candidate.TcpType));
            }

            return builder.ToString();
        }

        /// <summary>
        /// 取候选串里要写的端口：TCP 主动候选的真实端口由对端被动候选给出，因此固定写 9
        /// </summary>
        /// <param name="candidate">候选</param>
        /// <returns>候选串中的端口号</returns>
        private static int GetPort(Candidate candidate)
        {
            // RFC 6544 §4.5：主动候选不监听本地端口，写 9 表示由对端提供连接方向
            if (candidate.Transport == IceTransportType.Tcp && candidate.TcpType == TcpCandidateType.Active)
            {
                return ActivePlaceholderPort;
            }
            return candidate.Port;
        }

        /// <summary>
        /// 格式化地址：IPv6 去掉作用域标识且不加方括号，便于对端直接解析
        /// </summary>
        /// <param name="address">候选地址</param>
        /// <returns>地址文本</returns>
        private static string FormatAddress(IPAddress address)
        {
            if (address == null)
            {
                return string.Empty;
            }
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // 作用域标识只在本机有意义，跨主机传输后会误导对端
                return new IPAddress(address.GetAddressBytes()).ToString();
            }
            return address.ToString();
        }

        /// <summary>
        /// 取传输协议在候选串中的写法
        /// </summary>
        /// <param name="transport">传输协议</param>
        /// <returns>udp 或 tcp</returns>
        private static string GetTransportName(IceTransportType transport)
        {
            return transport == IceTransportType.Tcp ? "tcp" : "udp";
        }

        /// <summary>
        /// 取候选类型在候选串中的写法
        /// </summary>
        /// <param name="type">候选类型</param>
        /// <returns>host、srflx、prflx 或 relay</returns>
        private static string GetTypeName(CandidateType type)
        {
            if (type == CandidateType.ServerReflexive)
            {
                return "srflx";
            }
            if (type == CandidateType.PeerReflexive)
            {
                return "prflx";
            }
            if (type == CandidateType.Relay)
            {
                return "relay";
            }
            return "host";
        }

        /// <summary>
        /// 取 TCP 方向类型在候选串中的写法
        /// </summary>
        /// <param name="tcpType">TCP 方向类型</param>
        /// <returns>active、passive 或 so</returns>
        private static string GetTcpTypeName(TcpCandidateType tcpType)
        {
            if (tcpType == TcpCandidateType.Active)
            {
                return "active";
            }
            if (tcpType == TcpCandidateType.Passive)
            {
                return "passive";
            }
            return "so";
        }
    }
}