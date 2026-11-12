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
*文件名： IceSessionOptions
*版本号： v26.11.5.1
*唯一标识：8f28afaf-a337-4c97-9aff-c34f03bc6ba1
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 14:24:38
*描述：ICE 会话与选路阶梯预算配置
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 16:36:13
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN over TLS 证书校验配置
*
*****************************************************************************/
using System;

using SAEA.P2P.Turn;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// ICE 会话配置：收集预算、五级选路阶段预算与会话总预算，叠加 ICE 与 TURN 两个子配置
    /// </summary>
    public sealed class IceSessionOptions
    {
        /// <summary>
        /// 候选收集预算
        /// </summary>
        private TimeSpan _gatheringBudget = TimeSpan.FromSeconds(3);

        /// <summary>
        /// UDP 直连阶段预算
        /// </summary>
        private TimeSpan _udpDirectBudget = TimeSpan.FromSeconds(10);

        /// <summary>
        /// TCP 直连阶段预算
        /// </summary>
        private TimeSpan _tcpDirectBudget = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 单个 TURN 阶段预算，三个传输级别共用
        /// </summary>
        private TimeSpan _turnStageBudget = TimeSpan.FromSeconds(15);

        /// <summary>
        /// 会话总预算
        /// </summary>
        private TimeSpan _sessionBudget = TimeSpan.FromSeconds(90);

        /// <summary>
        /// ICE 配置
        /// </summary>
        private IceOptions _ice = new IceOptions();

        /// <summary>
        /// TURN 配置
        /// </summary>
        private TurnClientOptions _turn;

        /// <summary>
        /// TURN over TLS 的证书校验与 SNI 目标主机名
        /// </summary>
        private string _turnTlsTargetHost;

        /// <summary>
        /// 是否忽略 TURN over TLS 的证书错误
        /// </summary>
        private bool _turnTlsAllowInvalidCertificates;

        /// <summary>
        /// 候选收集预算（阶段一），默认 3s
        /// </summary>
        public TimeSpan GatheringBudget
        {
            get { return _gatheringBudget; }
            set { _gatheringBudget = value; }
        }

        /// <summary>
        /// UDP 直连阶段预算，默认 10s
        /// </summary>
        public TimeSpan UdpDirectBudget
        {
            get { return _udpDirectBudget; }
            set { _udpDirectBudget = value; }
        }

        /// <summary>
        /// TCP 直连阶段预算，默认 10s
        /// </summary>
        public TimeSpan TcpDirectBudget
        {
            get { return _tcpDirectBudget; }
            set { _tcpDirectBudget = value; }
        }

        /// <summary>
        /// TURN 阶段预算，TURN over UDP、TCP、TLS 三级共用，默认 15s
        /// </summary>
        public TimeSpan TurnStageBudget
        {
            get { return _turnStageBudget; }
            set { _turnStageBudget = value; }
        }

        /// <summary>
        /// 会话总预算，默认 90s；必须不小于收集预算与各级阶段预算之和
        /// </summary>
        public TimeSpan SessionBudget
        {
            get { return _sessionBudget; }
            set { _sessionBudget = value; }
        }

        /// <summary>
        /// ICE 配置，默认新建实例；为 null 时由校验拒绝
        /// </summary>
        public IceOptions Ice
        {
            get { return _ice; }
            set { _ice = value; }
        }

        /// <summary>
        /// TURN 配置；为 null 时 TURN 三个阶段不可用
        /// </summary>
        public TurnClientOptions Turn
        {
            get { return _turn; }
            set { _turn = value; }
        }

        /// <summary>
        /// TURN over TLS 的证书校验与 SNI 目标主机名；为 null 或空时取 TURN 服务端端点地址的字符串形式
        /// </summary>
        public string TurnTlsTargetHost
        {
            get { return _turnTlsTargetHost; }
            set { _turnTlsTargetHost = value; }
        }

        /// <summary>
        /// 是否忽略 TURN over TLS 的证书错误，仅本地自动化测试开启，默认 false
        /// </summary>
        public bool TurnTlsAllowInvalidCertificates
        {
            get { return _turnTlsAllowInvalidCertificates; }
            set { _turnTlsAllowInvalidCertificates = value; }
        }

        /// <summary>
        /// 五级阶段预算之和，TURN 的三级按三倍计入
        /// </summary>
        public TimeSpan StageBudgetTotal
        {
            get
            {
                // netstandard2.0 没有 TimeSpan 乘法运算符，按 Ticks 放大三倍
                return _udpDirectBudget
                    + _tcpDirectBudget
                    + TimeSpan.FromTicks(_turnStageBudget.Ticks * 3L);
            }
        }

        /// <summary>
        /// 取指定阶段可用的时间预算
        /// </summary>
        /// <param name="stage">选路阶段</param>
        /// <returns>该阶段的预算：UDP/TCP 直连各取自身配置，三个 TURN 阶段共用 TURN 阶段预算</returns>
        /// <exception cref="ArgumentOutOfRangeException">阶段取值不在枚举内</exception>
        public TimeSpan GetStageBudget(IceStage stage)
        {
            switch (stage)
            {
                case IceStage.UdpDirect:
                    return _udpDirectBudget;
                case IceStage.TcpDirect:
                    return _tcpDirectBudget;
                case IceStage.TurnUdp:
                case IceStage.TurnTcp:
                case IceStage.TurnTls:
                    return _turnStageBudget;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stage), "未知的选路阶段：" + stage);
            }
        }

        /// <summary>
        /// 校验预算组合与必填项，由选路阶梯启动时调用
        /// </summary>
        /// <exception cref="ArgumentException">ICE 配置为空，会话总预算小于收集预算与各级阶段预算之和，或 TURN over TLS 目标主机名为空白</exception>
        /// <exception cref="ArgumentOutOfRangeException">收集预算为负数，或某一级阶段预算、会话总预算不是正数</exception>
        public void Validate()
        {
            if (_ice == null)
            {
                throw new ArgumentException("ICE 配置不能为空", nameof(Ice));
            }
            if (_gatheringBudget < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(GatheringBudget), "收集预算不能为负数");
            }
            if (_udpDirectBudget <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(UdpDirectBudget), "UDP 直连阶段预算必须大于 0");
            }
            if (_tcpDirectBudget <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(TcpDirectBudget), "TCP 直连阶段预算必须大于 0");
            }
            if (_turnStageBudget <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(TurnStageBudget), "TURN 阶段预算必须大于 0");
            }
            if (_sessionBudget <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(SessionBudget), "会话总预算必须大于 0");
            }

            TimeSpan minimum = _gatheringBudget + StageBudgetTotal;
            if (_sessionBudget < minimum)
            {
                throw new ArgumentException("会话总预算不能小于收集预算与各级阶段预算之和：" + minimum, nameof(SessionBudget));
            }

            if (_turnTlsTargetHost != null && _turnTlsTargetHost.Trim().Length == 0)
            {
                throw new ArgumentException("TURN over TLS 目标主机名不能为空白", nameof(TurnTlsTargetHost));
            }
        }
    }
}