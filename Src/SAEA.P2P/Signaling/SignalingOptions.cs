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
*文件名： SignalingOptions
*版本号： v26.11.5.1
*唯一标识：df6b0905-8e4c-4b9b-b45a-69ed49477de7
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 09:53:18
*描述：信令客户端配置
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 09:53:18
*修改人： yswenli
*版本号： v26.11.5.1
*描述：信令客户端配置
*
*****************************************************************************/
using SAEA.P2P.Common;

namespace SAEA.P2P.Signaling
{
    /// <summary>
    /// 信令客户端配置，包含服务器地址、TLS、超时、重连、心跳与节点身份
    /// </summary>
    public class SignalingOptions
    {
        /// <summary>
        /// 信令服务器地址，默认 127.0.0.1
        /// </summary>
        public string ServerAddress { get; set; } = "127.0.0.1";

        /// <summary>
        /// 信令服务器端口，默认 39654，取值 1-65535
        /// </summary>
        public int ServerPort { get; set; } = 39654;

        /// <summary>
        /// 是否用 TLS 连接信令服务器，默认关闭
        /// </summary>
        public bool EnableTls { get; set; } = false;

        /// <summary>
        /// TLS 证书校验用的目标主机名；为空时回退为 ServerAddress
        /// </summary>
        public string TlsTargetHost { get; set; } = null;

        /// <summary>
        /// 是否接受不受信任的服务端证书，默认关闭，仅调试环境使用
        /// </summary>
        public bool TlsAllowInvalidCertificates { get; set; } = false;

        /// <summary>
        /// 建立 TCP 连接的超时，单位毫秒，默认 10000
        /// </summary>
        public int ConnectTimeoutMs { get; set; } = 10000;

        /// <summary>
        /// 等待 RegisterAck 的超时，单位毫秒，默认 10000
        /// </summary>
        public int RegisterTimeoutMs { get; set; } = 10000;

        /// <summary>
        /// 断线后的重连间隔，单位毫秒，默认 2000
        /// </summary>
        public int ReconnectIntervalMs { get; set; } = 2000;

        /// <summary>
        /// 最大重连次数，默认 0 表示不限次数
        /// </summary>
        public int MaxReconnectAttempts { get; set; } = 0;

        /// <summary>
        /// 心跳发送间隔，单位毫秒，默认 30000，需小于服务端空闲回收时间
        /// </summary>
        public int HeartbeatIntervalMs { get; set; } = 30000;

        /// <summary>
        /// 本端节点标识，必填
        /// </summary>
        public string PeerId { get; set; } = null;

        /// <summary>
        /// 节点名称；为空时注册报文使用 PeerId
        /// </summary>
        public string NodeName { get; set; } = null;

        /// <summary>
        /// 能力标签；为空时注册报文下发 udp、tcp
        /// </summary>
        public string[] Capabilities { get; set; } = null;

        /// <summary>
        /// 与服务端一致的共享密钥；为空时不对鉴权挑战作应答
        /// </summary>
        public string SharedSecret { get; set; } = null;

        /// <summary>
        /// 校验配置合法性，非法时抛出 P2PException；异常消息只含字段名与约束，不含凭据
        /// </summary>
        /// <exception cref="P2PException">PeerId、地址、端口、超时、间隔或重连次数非法</exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(PeerId))
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "PeerId is required");
            }

            if (string.IsNullOrWhiteSpace(ServerAddress))
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "ServerAddress is required");
            }

            if (ServerPort <= 0 || ServerPort > 65535)
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "ServerPort must be between 1 and 65535");
            }

            if (ConnectTimeoutMs <= 0)
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "ConnectTimeoutMs must be greater than 0");
            }

            if (RegisterTimeoutMs <= 0)
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "RegisterTimeoutMs must be greater than 0");
            }

            if (ReconnectIntervalMs <= 0)
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "ReconnectIntervalMs must be greater than 0");
            }

            if (HeartbeatIntervalMs <= 0)
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "HeartbeatIntervalMs must be greater than 0");
            }

            if (MaxReconnectAttempts < 0)
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "MaxReconnectAttempts cannot be negative");
            }
        }
    }
}