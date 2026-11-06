/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.P2P.NAT
*文件名： NATDetector
*版本号： v26.4.23.1
*唯一标识：f638c2bb-e7f1-4872-956d-c51c0c8a7058
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:07:31
*描述：NATDetector检测器类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:20:16
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SAEA.P2P.NAT
{
    /// <summary>
    /// NAT 检测器，记录本机地址与公网地址，并按探测结果推断 NAT 类型和打洞可行性
    /// </summary>
    public class NATDetector
    {
        /// <summary>
        /// 探测得到的 NAT 类型，默认 Unknown
        /// </summary>
        public NATType DetectedNATType { get; private set; } = NATType.Unknown;
        /// <summary>
        /// 记录的公网地址，仅在 SetPublicAddress 调用后非 null
        /// </summary>
        public IPEndPoint PublicAddress { get; private set; }
        /// <summary>
        /// 构造时探测到的本机出口地址
        /// </summary>
        public IPEndPoint LocalAddress { get; private set; }
        
        /// <summary>
        /// 初始化检测器并探测本机出口地址
        /// </summary>
        public NATDetector()
        {
            LocalAddress = GetLocalAddress();
        }
        
        /// <summary>
        /// 探测本机 NAT 类型；当前实现不做任何网络探测，直接置为全锥型
        /// </summary>
        /// <param name="serverAddr">探测服务器地址，当前实现未使用</param>
        /// <param name="serverPort">探测服务器端口，当前实现未使用</param>
        /// <returns>探测到的 NAT 类型，当前恒为 FullCone</returns>
        public async Task<NATType> DetectAsync(string serverAddr, int serverPort)
        {
            DetectedNATType = NATType.FullCone;
            return DetectedNATType;
        }
        
        /// <summary>
        /// 按两个探测服务器观察到的公网地址推断 NAT 类型，不修改 DetectedNATType
        /// </summary>
        /// <param name="firstPublic">第一个探测服务器观察到的公网地址</param>
        /// <param name="secondPublic">第二个探测服务器观察到的公网地址</param>
        /// <returns>NAT 类型；任一参数为 null 时返回 Unknown，地址与端口全同返回 FullCone，仅地址相同返回 RestrictedCone，否则返回 Symmetric</returns>
        public NATType DetectFromProbeResponses(IPEndPoint firstPublic, IPEndPoint secondPublic)
        {
            if (firstPublic == null || secondPublic == null)
                return NATType.Unknown;
            
            if (firstPublic.Equals(secondPublic))
                return NATType.FullCone;
            
            if (firstPublic.Address.Equals(secondPublic.Address))
                return NATType.RestrictedCone;
            
            return NATType.Symmetric;
        }
        
        /// <summary>
        /// 记录公网地址，通常取自服务器回传的探测结果
        /// </summary>
        /// <param name="publicAddr">公网 IPEndPoint</param>
        public void SetPublicAddress(IPEndPoint publicAddr)
        {
            PublicAddress = publicAddr;
        }
        
        /// <summary>
        /// 覆盖检测到的 NAT 类型
        /// </summary>
        /// <param name="natType">要设置的 NAT 类型</param>
        public void SetNATType(NATType natType)
        {
            DetectedNATType = natType;
        }
        
        private IPEndPoint GetLocalAddress()
        {
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect("8.8.8.8", 80);
                    return (IPEndPoint)socket.LocalEndPoint;
                }
            }
            catch
            {
                // Offline / no route: fall back to a local interface address.
                try
                {
                    var host = Dns.GetHostName();
                    var addresses = Dns.GetHostAddresses(host);
                    foreach (var address in addresses)
                    {
                        if (address.AddressFamily == AddressFamily.InterNetwork)
                            return new IPEndPoint(address, 0);
                    }
                }
                catch
                {
                    // Ignore and use loopback.
                }
                
                return new IPEndPoint(IPAddress.Loopback, 0);
            }
        }
        
        /// <summary>
        /// 按本端 NAT 类型判断与对端能否打洞：本端全锥型恒可，本端对称型仅可与全锥型对端打洞，本端 Unknown 一律不可
        /// </summary>
        /// <param name="otherNat">对端 NAT 类型</param>
        /// <returns>可打洞返回 true，否则返回 false</returns>
        public bool CanPunchWith(NATType otherNat)
        {
            switch (DetectedNATType)
            {
                case NATType.FullCone:
                    return true;
                case NATType.RestrictedCone:
                    return otherNat != NATType.Symmetric;
                case NATType.PortRestrictedCone:
                    return otherNat != NATType.Symmetric;
                case NATType.Symmetric:
                    return otherNat == NATType.FullCone;
                default:
                    return false;
            }
        }
    }
}