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
*修改时间：2026/11/6 12:20:16
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
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
    /// <summary>
    /// UDP 打洞执行器，按策略判定与对端的可达性，并向目标地址发送 PunchSync 探测包
    /// </summary>
    public class HolePuncher
    {
        private P2PCoder _coder = new P2PCoder();
        private HolePunchStrategy _strategy;
        private int _timeout;
        private int _maxRetry;
        private NATDetector _detector;
        
        /// <summary>
        /// 收到对端 PunchSync 包时触发，参数为来源地址与包负载副本
        /// </summary>
        public event Action<IPEndPoint, byte[]> OnPunchPacketReceived;
        /// <summary>
        /// 打洞结束通知最终结果；当前实现未在类内触发该事件
        /// </summary>
        public event Action<HolePunchResult> OnHolePunchCompleted;
        
        /// <summary>
        /// 初始化打洞器并创建内置 NAT 检测器
        /// </summary>
        /// <param name="strategy">打洞策略，默认优先直连</param>
        /// <param name="timeout">打洞总超时时间，单位毫秒，默认 10000</param>
        /// <param name="maxRetry">PunchSync 最大发送次数，默认 3</param>
        public HolePuncher(HolePunchStrategy strategy = HolePunchStrategy.PreferDirect, int timeout = 10000, int maxRetry = 3)
        {
            _strategy = strategy;
            _timeout = timeout;
            _maxRetry = maxRetry;
            _detector = new NATDetector();
        }
        
        /// <summary>
        /// 读取内置检测器当前的 NAT 类型
        /// </summary>
        /// <returns>当前 NAT 类型；未探测时为 Unknown</returns>
        public NATType GetNATType() => _detector.DetectedNATType;
        /// <summary>
        /// 读取内置检测器记录的公网地址
        /// </summary>
        /// <returns>公网 IPEndPoint；尚未设置时为 null</returns>
        public IPEndPoint GetPublicAddress() => _detector.PublicAddress;
        /// <summary>
        /// 读取内置检测器探测到的本机地址
        /// </summary>
        /// <returns>本机 IPEndPoint；离线时回退到本机网卡地址或回环地址，端口为 0</returns>
        public IPEndPoint GetLocalAddress() => _detector.LocalAddress;
        
        /// <summary>
        /// 更新内置检测器的公网地址
        /// </summary>
        /// <param name="addr">公网 IPEndPoint，通常取自 NAT 探测结果</param>
        public void SetPublicAddress(IPEndPoint addr) => _detector.SetPublicAddress(addr);
        /// <summary>
        /// 覆盖内置检测器的 NAT 类型
        /// </summary>
        /// <param name="nat">要设置的 NAT 类型</param>
        public void SetNATType(NATType nat) => _detector.SetNATType(nat);
        
        /// <summary>
        /// 按策略预判与目标 NAT 类型能否直连：RelayOnly 恒失败，DirectOnly 在 NAT 不兼容时失败
        /// </summary>
        /// <param name="targetNat">对端 NAT 类型</param>
        /// <returns>null 表示允许尝试打洞，否则为预判的失败结果</returns>
        public HolePunchResult CanPunchWith(NATType targetNat)
        {
            if (_strategy == HolePunchStrategy.RelayOnly)
                return HolePunchResult.Failed("RelayOnly strategy", _detector.DetectedNATType, targetNat);
            
            if (_strategy == HolePunchStrategy.DirectOnly && !_detector.CanPunchWith(targetNat))
                return HolePunchResult.Failed("NAT incompatible", _detector.DetectedNATType, targetNat);
            
            return null;
        }
        
        /// <summary>
        /// 在超时与重试上限内循环向目标发送 PunchSync，每次间隔 100 毫秒
        /// </summary>
        /// <param name="socket">用于发送 UDP 包的客户端 socket</param>
        /// <param name="targetAddr">打洞目标地址</param>
        /// <param name="sessionId">会话标识，与当前时间戳拼接后作为包负载</param>
        /// <returns>打洞结果；当前实现不等待对端响应，只在 RelayOnly 策略或重试耗尽时返回失败</returns>
        public async Task<HolePunchResult> PunchAsync(SaeaUdpClientSocket socket, IPEndPoint targetAddr, string sessionId)
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
        
        /// <summary>
        /// 解析收到的 UDP 数据，对其中每个 PunchSync 帧触发 OnPunchPacketReceived
        /// </summary>
        /// <param name="data">收到的原始帧数据，可含未完成的半包</param>
        /// <param name="source">数据来源地址，原样传给事件</param>
        public void ProcessPunchSync(byte[] data, IPEndPoint source)
        {
            using (var frames = _coder.DecodeP2P(data))
            {
                foreach (var frame in frames.Frames)
                {
                    if ((P2PMessageType)frame.Type == P2PMessageType.PunchSync)
                    {
                        OnPunchPacketReceived?.Invoke(source, frame.Content.ToArray());
                    }
                }
            }
        }
    }
}