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
*文件名： IceSessionResult
*版本号： v26.11.5.1
*唯一标识：53ac2aa0-8314-4e1f-a511-056557c3196b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 14:44:17
*描述：ICE 选路结果
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 10:21:07
*修改人： yswenli
*版本号： v26.11.5.1
*描述：通道类型改为 ICE 选路结果
*
*****************************************************************************/
using System;
using System.Net;

using SAEA.P2P.Channel;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// ICE 选路结果：打通的阶段、选中的候选对与通道类型，端点由选中候选对导出。
    /// </summary>
    public sealed class IceSessionResult
    {
        /// <summary>
        /// 用已打通的阶段与选中候选对创建结果，由会话内部调用
        /// </summary>
        /// <param name="stage">打通的阶段</param>
        /// <param name="pair">选中的候选对</param>
        internal IceSessionResult(IceStage stage, SelectedPair pair)
        {
            Stage = stage;
            Pair = pair;
        }

        /// <summary>
        /// 打通的阶段，即回退阶梯停在哪一级
        /// </summary>
        public IceStage Stage { get; }

        /// <summary>
        /// 选中的候选对：未选出时为 null，此时端点与传输标记都为空或 false
        /// </summary>
        public SelectedPair Pair { get; }

        /// <summary>
        /// 通道类型：按打通的阶段映射，未选出候选对时返回默认值 UdpDirect
        /// </summary>
        public ChannelType ChannelType
        {
            get
            {
                if (Pair == null)
                {
                    return ChannelType.UdpDirect;
                }

                switch (Stage)
                {
                    case IceStage.TcpDirect:
                        return ChannelType.TcpDirect;
                    case IceStage.TurnUdp:
                        return ChannelType.RelayUdp;
                    case IceStage.TurnTcp:
                        return ChannelType.RelayTcp;
                    case IceStage.TurnTls:
                        return ChannelType.RelayTls;
                    default:
                        return ChannelType.UdpDirect;
                }
            }
        }

        /// <summary>
        /// 是否经中继转发
        /// </summary>
        public bool IsRelay
        {
            get { return Pair != null && Pair.IsRelay; }
        }

        /// <summary>
        /// 是否走 TCP
        /// </summary>
        public bool UsesTcp
        {
            get { return Pair != null && Pair.UsesTcp; }
        }

        /// <summary>
        /// 本端端点，由选中候选对导出；未选出时为 null
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get { return Pair != null ? Pair.Pair.Local.EndPoint : null; }
        }

        /// <summary>
        /// 对端端点，由选中候选对导出；未选出时为 null
        /// </summary>
        public IPEndPoint RemoteEndPoint
        {
            get { return Pair != null ? Pair.Pair.Remote.EndPoint : null; }
        }

        /// <summary>
        /// 输出阶段、通道类型与两端端点
        /// </summary>
        /// <returns>选路结果的文本描述</returns>
        public override string ToString()
        {
            if (Pair == null)
            {
                return string.Format("{0} {1} (no pair)", Stage, ChannelType);
            }
            return string.Format("{0} {1} {2} -> {3}", Stage, ChannelType, LocalEndPoint, RemoteEndPoint);
        }
    }
}
