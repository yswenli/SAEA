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
*命名空间：SAEA.P2P.Common
*文件名： ErrorCode
*版本号： v26.4.23.1
*唯一标识：a1818609-4ce1-4dc3-b6ba-e9cab12900eb
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 15:38:24
*描述：ErrorCode代码枚举
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 14:44:17
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增未连通错误码 EI03
*
*****************************************************************************/
namespace SAEA.P2P.Common
{
    /// <summary>
    /// P2P 错误码常量：按阶段用固定前缀编号，便于日志检索与对端识别
    /// </summary>
    public static class ErrorCode
    {
        /// <summary>
        /// 注册请求缺少对端标识
        /// </summary>
        public static readonly string RegisterPeerIdMissing = "EP01";
        /// <summary>
        /// 注册请求中的对端标识非法
        /// </summary>
        public static readonly string RegisterPeerIdInvalid = "EP02";
        /// <summary>
        /// 对端标识已被注册
        /// </summary>
        public static readonly string RegisterPeerIdDuplicate = "EP03";
        /// <summary>
        /// 注册请求缺少 NAT 信息
        /// </summary>
        public static readonly string RegisterNATInfoMissing = "EP04";
        /// <summary>
        /// 注册超时
        /// </summary>
        public static readonly string RegisterTimeout = "EP05";
        /// <summary>
        /// 注册失败
        /// </summary>
        public static readonly string RegisterFailed = "EP06";
        /// <summary>
        /// 注册服务器不可用
        /// </summary>
        public static readonly string RegisterServerUnavailable = "EP07";
        /// <summary>
        /// 注册鉴权失败
        /// </summary>
        public static readonly string RegisterAuthFailed = "EP08";
        /// <summary>
        /// 注册被拒绝
        /// </summary>
        public static readonly string RegisterRejected = "EP09";

        /// <summary>
        /// 发现阶段无响应
        /// </summary>
        public static readonly string DiscoveryNoResponse = "ED01";
        /// <summary>
        /// 发现阶段超时
        /// </summary>
        public static readonly string DiscoveryTimeout = "ED02";
        /// <summary>
        /// 发现阶段没有可用候选地址
        /// </summary>
        public static readonly string DiscoveryNoCandidates = "ED03";
        /// <summary>
        /// NAT 类型探测失败
        /// </summary>
        public static readonly string DiscoveryNatDetectionFailed = "ED04";
        /// <summary>
        /// 发现阶段收到的地址非法
        /// </summary>
        public static readonly string DiscoveryAddressInvalid = "ED05";
        /// <summary>
        /// 发现阶段端口被阻断
        /// </summary>
        public static readonly string DiscoveryPortBlocked = "ED06";
        /// <summary>
        /// 发现失败
        /// </summary>
        public static readonly string DiscoveryFailed = "ED07";

        /// <summary>
        /// 打洞失败
        /// </summary>
        public static readonly string PunchFailed = "EO01";
        /// <summary>
        /// 打洞超时
        /// </summary>
        public static readonly string PunchTimeout = "EO02";
        /// <summary>
        /// 到对端没有可用路由
        /// </summary>
        public static readonly string PunchNoRoute = "EO03";

        /// <summary>
        /// UDP 打洞失败
        /// </summary>
        public static readonly string HolePunchFailed = "EH01";
        /// <summary>
        /// UDP 打洞超时
        /// </summary>
        public static readonly string HolePunchTimeout = "EH02";

        /// <summary>
        /// 中继连接失败
        /// </summary>
        public static readonly string RelayFailed = "ER01";
        /// <summary>
        /// 中继服务器不可用
        /// </summary>
        public static readonly string RelayServerUnavailable = "ER02";
        /// <summary>
        /// 中继会话不存在
        /// </summary>
        public static readonly string RelaySessionNotFound = "ER03";
        /// <summary>
        /// 中继配额超限
        /// </summary>
        public static readonly string RelayQuotaExceeded = "ER04";

        /// <summary>
        /// 鉴权失败
        /// </summary>
        public static readonly string AuthFailed = "EA01";
        /// <summary>
        /// 鉴权超时
        /// </summary>
        public static readonly string AuthTimeout = "EA03";

        /// <summary>
        /// 加密失败
        /// </summary>
        public static readonly string EncryptionFailed = "EE01";
        /// <summary>
        /// 解密失败
        /// </summary>
        public static readonly string DecryptionFailed = "EE02";

        // 数据面：业务帧长度与首字节解复用相关错误
        /// <summary>
        /// 业务帧超过单包上限
        /// </summary>
        public static readonly string PayloadTooLarge = "EF01";

        // ICE 选路：阶梯耗尽与总预算超时
        /// <summary>
        /// 所有选路阶段均失败，没有可用的候选对
        /// </summary>
        public static readonly string NoCandidatePair = "EI01";
        /// <summary>
        /// ICE 选路会话超出总预算
        /// </summary>
        public static readonly string IceTimeout = "EI02";
        /// <summary>
        /// 尚未选出可用通道就尝试发送业务数据
        /// </summary>
        public static readonly string NotConnected = "EI03";

        // TURN 回退：服务不可用与鉴权失败
        /// <summary>
        /// TURN 服务器不可用
        /// </summary>
        public static readonly string TurnUnavailable = "ET01";
        /// <summary>
        /// TURN 鉴权失败
        /// </summary>
        public static readonly string TurnAuthFailed = "ET02";

        private static readonly System.Collections.Generic.Dictionary<string, string> Descriptions =
            new System.Collections.Generic.Dictionary<string, string>
            {
                { RegisterPeerIdMissing, "Peer ID missing in registration request" },
                { RegisterPeerIdInvalid, "Peer ID invalid" },
                { RegisterPeerIdDuplicate, "Peer ID already registered" },
                { RegisterNATInfoMissing, "NAT info missing in registration request" },
                { RegisterTimeout, "Registration timeout" },
                { RegisterFailed, "Registration failed" },
                { RegisterServerUnavailable, "Registration server unavailable" },
                { RegisterAuthFailed, "Registration authentication failed" },
                { RegisterRejected, "Registration rejected" },
                { DiscoveryNoResponse, "Discovery no response" },
                { DiscoveryTimeout, "Discovery timeout" },
                { DiscoveryNoCandidates, "Discovery no candidates found" },
                { DiscoveryNatDetectionFailed, "NAT detection failed" },
                { DiscoveryAddressInvalid, "Discovery address invalid" },
                { DiscoveryPortBlocked, "Discovery port blocked" },
                { DiscoveryFailed, "Discovery failed" },
                { PunchFailed, "Hole punch failed" },
                { PunchTimeout, "Hole punch timeout" },
                { PunchNoRoute, "No route to peer" },
                { HolePunchFailed, "Hole punch failed" },
                { HolePunchTimeout, "Hole punch timeout" },
                { RelayFailed, "Relay connection failed" },
                { RelayServerUnavailable, "Relay server unavailable" },
                { RelaySessionNotFound, "Relay session not found" },
                { RelayQuotaExceeded, "Relay quota exceeded" },
                { AuthFailed, "Authentication failed" },
                { AuthTimeout, "Authentication timeout" },
                { EncryptionFailed, "Encryption failed" },
                { DecryptionFailed, "Decryption failed" },
                { PayloadTooLarge, "Payload too large" },
                { NoCandidatePair, "No candidate pair" },
                { IceTimeout, "ICE session timeout" },
                { NotConnected, "ICE channel not selected" },
                { TurnUnavailable, "TURN server unavailable" },
                { TurnAuthFailed, "TURN authentication failed" }
            };

        /// <summary>获取错误码对应的英文描述；未登记的错误码返回 Unknown error</summary>
        /// <param name="errorCode">错误码常量，如 EP01</param>
        /// <returns>描述文本；未登记或为空时返回 Unknown error</returns>
        public static string GetDescription(string errorCode)
        {
            if (!string.IsNullOrEmpty(errorCode) && Descriptions.TryGetValue(errorCode, out var description))
            {
                return description;
            }
            return "Unknown error";
        }
    }
}