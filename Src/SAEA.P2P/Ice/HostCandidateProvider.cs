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
*文件名： HostCandidateProvider
*版本号： v26.11.5.1
*唯一标识：e434ab22-8bec-4724-9fd2-3dac8c160787
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 12:15:00
*描述：主机候选地址收集
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 12:15:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增主机候选地址收集
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// 主机候选地址提供者：按配置枚举本机地址，做地址族、回环与 v4-mapped 过滤后按固定顺序返回
    /// </summary>
    public sealed class HostCandidateProvider
    {
        /// <summary>
        /// 收集配置
        /// </summary>
        private readonly IceOptions _options;

        /// <summary>
        /// 用给定配置创建提供者
        /// </summary>
        /// <param name="options">收集配置</param>
        /// <exception cref="ArgumentNullException">配置为空</exception>
        public HostCandidateProvider(IceOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            _options = options;
        }

        /// <summary>
        /// 收集本机可用于主机候选的地址
        /// </summary>
        /// <returns>过滤并排序后的地址列表；无可用地址时为空列表</returns>
        public IReadOnlyList<IPAddress> GetAddresses()
        {
            var result = new List<IPAddress>();
            var bindAddresses = _options.BindAddresses;

            if (bindAddresses != null && bindAddresses.Count > 0)
            {
                for (int i = 0; i < bindAddresses.Count; i++)
                {
                    AddCandidate(result, bindAddresses[i]);
                }
            }
            else
            {
                NetworkInterface[] interfaces;
                try
                {
                    interfaces = NetworkInterface.GetAllNetworkInterfaces();
                }
                catch (NetworkInformationException)
                {
                    // 受限环境下枚举网卡会失败，收集阶段按无可用地址处理，不向上抛
                    return result;
                }

                for (int i = 0; i < interfaces.Length; i++)
                {
                    AddInterfaceCandidates(result, interfaces[i]);
                }
            }

            SortAddresses(result);
            return result;
        }

        /// <summary>
        /// 判断给定地址是否为链路本地地址：IPv4 为 169.254/16，IPv6 为 fe80::/10
        /// </summary>
        /// <param name="address">待判断的地址，为空时返回 false</param>
        /// <returns>链路本地地址返回 true</returns>
        public static bool IsLinkLocal(IPAddress address)
        {
            if (address == null)
            {
                return false;
            }
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return address.IsIPv6LinkLocal;
            }
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                byte[] bytes = address.GetAddressBytes();
                return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
            }
            return false;
        }

        /// <summary>
        /// 加入单个网卡上的地址
        /// </summary>
        /// <param name="result">结果列表</param>
        /// <param name="networkInterface">网卡</param>
        private void AddInterfaceCandidates(List<IPAddress> result, NetworkInterface networkInterface)
        {
            if (networkInterface == null)
            {
                return;
            }
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                return;
            }
            if (!_options.AllowLoopbackCandidates && networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                return;
            }

            UnicastIPAddressInformationCollection unicastAddresses;
            try
            {
                unicastAddresses = networkInterface.GetIPProperties().UnicastAddresses;
            }
            catch (NetworkInformationException)
            {
                return;
            }

            for (int i = 0; i < unicastAddresses.Count; i++)
            {
                AddCandidate(result, unicastAddresses[i].Address);
            }
        }

        /// <summary>
        /// 归一化并过滤后加入结果，已在结果中的地址不重复加入
        /// </summary>
        /// <param name="result">结果列表</param>
        /// <param name="address">候选地址</param>
        private void AddCandidate(List<IPAddress> result, IPAddress address)
        {
            if (address == null)
            {
                return;
            }

            // v4-mapped 地址与对应 IPv4 视为同一地址，必须先归一化再判断与去重
            IPAddress normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
            if (result.Contains(normalized))
            {
                return;
            }
            if (IsAllowed(normalized))
            {
                result.Add(normalized);
            }
        }

        /// <summary>
        /// 判断地址是否允许作为主机候选
        /// </summary>
        /// <param name="address">已归一化的地址</param>
        /// <returns>允许返回 true</returns>
        private bool IsAllowed(IPAddress address)
        {
            if (address == null)
            {
                return false;
            }
            if (address.AddressFamily != AddressFamily.InterNetwork && address.AddressFamily != AddressFamily.InterNetworkV6)
            {
                return false;
            }
            if (!_options.EnableIpv6 && address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return false;
            }
            if (!_options.AllowLoopbackCandidates && IPAddress.IsLoopback(address))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 排序：IPv4 在前，同族按地址字节比较，最后按文本兜底
        /// </summary>
        /// <param name="addresses">待排序地址列表</param>
        private static void SortAddresses(List<IPAddress> addresses)
        {
            addresses.Sort(CompareAddresses);
        }

        /// <summary>
        /// 地址比较器，结果与调用时机无关，便于用例复现
        /// </summary>
        /// <param name="left">左侧地址</param>
        /// <param name="right">右侧地址</param>
        /// <returns>比较结果</returns>
        private static int CompareAddresses(IPAddress left, IPAddress right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }
            if (left == null)
            {
                return -1;
            }
            if (right == null)
            {
                return 1;
            }

            int leftRank = left.AddressFamily == AddressFamily.InterNetwork ? 0 : 1;
            int rightRank = right.AddressFamily == AddressFamily.InterNetwork ? 0 : 1;
            if (leftRank != rightRank)
            {
                return leftRank < rightRank ? -1 : 1;
            }

            byte[] leftBytes = left.GetAddressBytes();
            byte[] rightBytes = right.GetAddressBytes();
            int length = leftBytes.Length < rightBytes.Length ? leftBytes.Length : rightBytes.Length;
            for (int i = 0; i < length; i++)
            {
                if (leftBytes[i] != rightBytes[i])
                {
                    return leftBytes[i] < rightBytes[i] ? -1 : 1;
                }
            }
            if (leftBytes.Length != rightBytes.Length)
            {
                return leftBytes.Length < rightBytes.Length ? -1 : 1;
            }

            return string.CompareOrdinal(left.ToString(), right.ToString());
        }
    }
}