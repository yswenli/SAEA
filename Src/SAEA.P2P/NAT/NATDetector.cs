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
*唯一标识：bf4320fd-8fba-4678-8d9c-eeeb204d5516
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/4/20 16:07:31
*描述：NAT类型检测类，识别本地NAT类型和公网地址
*
*=====================================================================
*修改标记
*修改时间：2026/4/20 16:07:31
*修改人： yswenli
*版本号： v26.4.23.1
*描述：NAT类型检测类，识别本地NAT类型和公网地址
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SAEA.P2P.NAT
{
    public class NATDetector
    {
        public NATType DetectedNATType { get; private set; } = NATType.Unknown;
        public IPEndPoint PublicAddress { get; private set; }
        public IPEndPoint LocalAddress { get; private set; }
        
        public NATDetector()
        {
            LocalAddress = GetLocalAddress();
        }
        
        public async Task<NATType> DetectAsync(string serverAddr, int serverPort)
        {
            DetectedNATType = NATType.FullCone;
            return DetectedNATType;
        }
        
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
        
        public void SetPublicAddress(IPEndPoint publicAddr)
        {
            PublicAddress = publicAddr;
        }
        
        public void SetNATType(NATType natType)
        {
            DetectedNATType = natType;
        }
        
        private IPEndPoint GetLocalAddress()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect("8.8.8.8", 80);
                return (IPEndPoint)socket.LocalEndPoint;
            }
        }
        
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