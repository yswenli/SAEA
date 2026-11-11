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
*文件名： SelectedPair
*版本号： v26.11.5.1
*唯一标识：f33c1793-d478-4f5a-a873-6d63af2d571b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 17:00:00
*描述：ICE 提名成功后发布的选中候选对
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 17:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增选中候选对
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// 选中候选对：只有提名成功后才发布，业务数据只允许在发布的 pair 上发送。
    /// </summary>
    public sealed class SelectedPair
    {
        /// <summary>
        /// 用已完成提名的候选对创建选中结果
        /// </summary>
        /// <param name="pair">已提名的候选对</param>
        /// <exception cref="ArgumentNullException">候选对为空</exception>
        public SelectedPair(CandidatePair pair)
        {
            if (pair == null)
            {
                throw new ArgumentNullException(nameof(pair));
            }

            Pair = pair;
        }

        /// <summary>
        /// 被选中的候选对
        /// </summary>
        public CandidatePair Pair { get; }

        /// <summary>
        /// 是否经中继转发：业务数据仍在同一 pair 上发送，但中转点在中继侧
        /// </summary>
        public bool IsRelay
        {
            get { return Pair.Local.Type == CandidateType.Relay; }
        }

        /// <summary>
        /// 是否走 TCP：UDP 检查与 TCP 连接两种通道的保活方式不同，调用方据此分流
        /// </summary>
        public bool UsesTcp
        {
            get { return Pair.Local.Transport == IceTransportType.Tcp; }
        }

        /// <summary>
        /// 输出选中候选对的两端端点与中继标记
        /// </summary>
        /// <returns>选中候选对的文本描述</returns>
        public override string ToString()
        {
            return string.Format("selected {0} -> {1} relay={2}", Pair.Local.EndPoint, Pair.Remote.EndPoint, IsRelay);
        }
    }
}