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
*命名空间：SAEA.P2P.Signaling
*文件名： SignalingCandidate
*版本号： v26.11.5.1
*唯一标识：3c5afccb-c207-42b4-94fb-bbba33a36728
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:15:22
*描述：信令报文中的 ICE 候选传输模型
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 18:15:22
*修改人： yswenli
*版本号： v26.11.5.1
*描述：信令报文中的 ICE 候选传输模型
*
*****************************************************************************/
namespace SAEA.P2P.Signaling
{
    /// <summary>
    /// 信令报文中的 ICE 候选传输模型，独立于 ICE 内部候选类型，只承载报文所需的文本字段
    /// </summary>
    public class SignalingCandidate
    {
        /// <summary>
        /// 候选基础标识，相同基础地址与传输的候选共用
        /// </summary>
        public string Foundation { get; set; }

        /// <summary>
        /// 分量编号，从 1 开始
        /// </summary>
        public int Component { get; set; }

        /// <summary>
        /// 传输协议，取 udp 或 tcp
        /// </summary>
        public string Transport { get; set; }

        /// <summary>
        /// 候选优先级
        /// </summary>
        public uint Priority { get; set; }

        /// <summary>
        /// 候选地址文本，地址族由文本本身判定
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 候选端口
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 候选类型，取 host、srflx、prflx、relay
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 关联地址文本，无关联信息时为 null
        /// </summary>
        public string RelatedAddress { get; set; }

        /// <summary>
        /// 关联端口，无关联信息时为 0
        /// </summary>
        public int RelatedPort { get; set; }

        /// <summary>
        /// TCP 候选类型，取 active、passive、so；非 TCP 候选为 null
        /// </summary>
        public string TcpType { get; set; }
    }
}