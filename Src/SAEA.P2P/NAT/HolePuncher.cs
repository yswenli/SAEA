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
*文件名： HolePuncher
*版本号： v26.4.23.1
*唯一标识：286f96be-7ddd-4a8b-b116-648dac71167f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:07:31
*描述：HolePuncher打洞类
*
*=====================================================================
*修改标记
*修改时间：2026/04/20 16:07:31
*修改人： yswenli
*版本号： v26.4.23.1
*描述：HolePuncher打洞类
*
*****************************************************************************/
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SAEA.Sockets.Core.Udp;
using SAEA.P2P.Protocol;
using SAEA.P2P.Common;

namespace SAEA.P2P.NAT
{
    public class HolePuncher
    {
        private P2PCoder _coder = new P2PCoder();
        private HolePunchStrategy _strategy;
        private int _timeout;
        private int _maxRetry;
        private NATDetector _detector;
        
        public event Action<IPEndPoint, byte[]> OnPunchPacketReceived;
        public event Action<HolePunchResult> OnHolePunchCompleted;
        
        public HolePuncher(HolePunchStrategy strategy = HolePunchStrategy.PreferDirect, int timeout = 10000, int maxRetry = 3)
        {
            _strategy = strategy;
            _timeout = timeout;
            _maxRetry = maxRetry;
            _detector = new NATDetector();
        }
        
        public NATType GetNATType() => _detector.DetectedNATType;
        public IPEndPoint GetPublicAddress() => _detector.PublicAddress;
        public IPEndPoint GetLocalAddress() => _detector.LocalAddress;
        
        public void SetPublicAddress(IPEndPoint addr) => _detector.SetPublicAddress(addr);
        public void SetNATType(NATType nat) => _detector.SetNATType(nat);
        
        public HolePunchResult CanPunchWith(NATType targetNat)
        {
            if (_strategy == HolePunchStrategy.RelayOnly)
                return HolePunchResult.Failed("RelayOnly strategy", _detector.DetectedNATType, targetNat);
            
            if (_strategy == HolePunchStrategy.DirectOnly && !_detector.CanPunchWith(targetNat))
                return HolePunchResult.Failed("NAT incompatible", _detector.DetectedNATType, targetNat);
            
            return null;
        }
        
        public async Task<HolePunchResult> PunchAsync(UdpClientSocket socket, IPEndPoint targetAddr, string sessionId)
        {
            if (_strategy == HolePunchStrategy.RelayOnly)
                return HolePunchResult.Failed("RelayOnly strategy", _detector.DetectedNATType, NATType.Unknown);
            
            var attempts = 0;
            var startTime = DateTime.UtcNow;
            
            while (attempts < _maxRetry && (DateTime.UtcNow - startTime).TotalMilliseconds < _timeout)
            {
                var punchSync = _coder.EncodeP2P(P2PMessageType.PunchSync, 
                    Encoding.UTF8.GetBytes(sessionId + "|" + DateTime.UtcNow.Ticks));
                
                socket.SendAsync(targetAddr, punchSync);
                P2PLogHelper.Debug("HolePuncher", $"PunchSync sent to {targetAddr}, attempt {attempts + 1}");
                
                attempts++;
                await Task.Delay(100);
            }
            
            return HolePunchResult.Failed("Timeout waiting for response", _detector.DetectedNATType, NATType.Unknown);
        }
        
        public void ProcessPunchSync(byte[] data, IPEndPoint source)
        {
            var protocols = _coder.DecodeP2P(data);
            foreach (var p in protocols)
            {
                if (p.GetMessageType() == P2PMessageType.PunchSync)
                {
                    OnPunchPacketReceived?.Invoke(source, p.Content);
                }
            }
        }
    }
}