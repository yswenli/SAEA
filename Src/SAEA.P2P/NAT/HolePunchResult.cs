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
*文件名： HolePunchResult
*版本号： v26.4.23.1
*唯一标识：b6840544-c85f-4cbe-b3a8-2315effecfd0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:07:31
*描述：HolePunchResult结果类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:20:16
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.P2P.NAT
{
    /// <summary>
    /// 打洞结果，记录成败、双方 NAT 类型与已建立的直连地址
    /// </summary>
    public class HolePunchResult
    {
        /// <summary>
        /// 打洞是否成功
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// 本端 NAT 类型
        /// </summary>
        public NATType SourceNATType { get; set; }
        /// <summary>
        /// 对端 NAT 类型
        /// </summary>
        public NATType TargetNATType { get; set; }
        /// <summary>
        /// 打洞成功后可用于直连的对端地址；失败时为 null
        /// </summary>
        public System.Net.IPEndPoint EstablishedAddress { get; set; }
        /// <summary>
        /// 失败原因文本；成功时为 null
        /// </summary>
        public string ErrorMessage { get; set; }
        /// <summary>
        /// 本次打洞实际发送 PunchSync 的次数；失败构造时为 0
        /// </summary>
        public int Attempts { get; set; }
        
        /// <summary>
        /// 构造成功结果，记录对端地址、双方 NAT 类型与尝试次数
        /// </summary>
        /// <param name="address">打洞成功后可用于直连的对端地址</param>
        /// <param name="sourceNat">本端 NAT 类型</param>
        /// <param name="targetNat">对端 NAT 类型</param>
        /// <param name="attempts">实际发送 PunchSync 的次数</param>
        /// <returns>成功的结果实例，ErrorMessage 为 null</returns>
        public static HolePunchResult Succeeded(System.Net.IPEndPoint address, NATType sourceNat, NATType targetNat, int attempts)
        {
            return new HolePunchResult
            {
                Success = true,
                EstablishedAddress = address,
                SourceNATType = sourceNat,
                TargetNATType = targetNat,
                Attempts = attempts
            };
        }
        
        /// <summary>
        /// 构造失败结果，保留双方 NAT 类型供日志与策略判断
        /// </summary>
        /// <param name="error">失败原因文本</param>
        /// <param name="sourceNat">本端 NAT 类型</param>
        /// <param name="targetNat">对端 NAT 类型</param>
        /// <returns>失败的结果实例，Success 为 false、Attempts 为 0</returns>
        public static HolePunchResult Failed(string error, NATType sourceNat, NATType targetNat)
        {
            return new HolePunchResult
            {
                Success = false,
                ErrorMessage = error,
                SourceNATType = sourceNat,
                TargetNATType = targetNat
            };
        }
    }
}