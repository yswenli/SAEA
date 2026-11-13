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
*命名空间：SAEA.P2P.Builder
*文件名： P2PServerOptions
*版本号： v26.4.23.1
*唯一标识：4fdfd249-ff66-4ebb-92d0-240b12d31aad
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 15:44:01
*描述：P2PServerOptions接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 13:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：删除服务端自定义中继配置
*
*****************************************************************************/
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using SAEA.P2P.Common;

namespace SAEA.P2P.Builder
{
    /// <summary>
    /// P2P 信令服务端总配置，聚合绑定地址、端口、容量、TLS、超时与日志子配置
    /// </summary>
    public class P2PServerOptions
    {
        /// <summary>
        /// 服务端监听绑定的 IP，默认 0.0.0.0 表示全部网卡
        /// </summary>
        public string BindIP { get; set; } = "0.0.0.0";
        
        /// <summary>
        /// 监听端口，默认 39654，取值 1-65535
        /// </summary>
        public int Port { get; set; } = 39654;
        
        /// <summary>
        /// 最大同时在线节点数，默认 10000，必须大于 0
        /// </summary>
        public int MaxNodes { get; set; } = 10000;
        
        /// <summary>
        /// 服务端 TLS 配置
        /// </summary>
        public ServerTlsOptions Tls { get; set; } = new ServerTlsOptions();
        
        /// <summary>
        /// 服务端超时配置
        /// </summary>
        public ServerTimeoutOptions Timeout { get; set; } = new ServerTimeoutOptions();
        
        /// <summary>
        /// 日志输出配置，与客户端共用同一套日志参数
        /// </summary>
        public LoggingOptions Logging { get; set; } = new LoggingOptions();
        
        /// <summary>
        /// 与 TURN 共用的共享密钥；为空时不启用信令鉴权、也不签发 TURN 凭据
        /// </summary>
        public string SharedSecret { get; set; }
        
        /// <summary>
        /// TURN 认证域，与 TURN 服务端一致，默认 saea
        /// </summary>
        public string Realm { get; set; } = "saea";
        
        /// <summary>
        /// 下发 TURN 临时凭据的有效期，单位秒，默认 600，必须大于 0
        /// </summary>
        public int CredentialTtlSeconds { get; set; } = 600;
        
        /// <summary>
        /// 下发给客户端的 TURN 服务地址列表；为空时不下发凭据
        /// </summary>
        public string[] TurnUrls { get; set; }
        
        /// <summary>
        /// 同一 peerId 重复注册时是否拒绝新连接；默认 false 表示由新连接顶替旧连接
        /// </summary>
        public bool RejectDuplicatePeer { get; set; } = false;
        
        /// <summary>
        /// 校验绑定地址、端口、容量与各子配置，非法时抛 P2PException
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(BindIP))
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "BindIP is required");
            }
            
            if (Port <= 0 || Port > 65535)
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "Port must be between 1 and 65535");
            }
            
            if (MaxNodes <= 0)
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "MaxNodes must be greater than 0");
            }
            
            Tls?.Validate();
            Timeout?.Validate();

            if (CredentialTtlSeconds <= 0)
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "CredentialTtlSeconds must be greater than 0");
            }

            if (!string.IsNullOrEmpty(SharedSecret) && string.IsNullOrWhiteSpace(Realm))
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "Realm is required when SharedSecret is set");
            }
        }
    }
    
    /// <summary>
    /// 服务端 TLS 参数，控制启用开关、证书来源、协议版本与客户端证书要求
    /// </summary>
    public class ServerTlsOptions
    {
        /// <summary>
        /// 是否启用 TLS，默认关闭
        /// </summary>
        public bool Enabled { get; set; } = false;
        
        /// <summary>
        /// 服务端证书文件路径，未直接提供证书实例时使用
        /// </summary>
        public string CertificatePath { get; set; }
        
        /// <summary>
        /// 证书文件密码，可为空字符串
        /// </summary>
        public string CertificatePassword { get; set; }
        
        /// <summary>
        /// 已加载的服务端证书实例，TLS 启用时与证书文件路径至少提供一个
        /// </summary>
        public X509Certificate2 Certificate { get; set; }
        
        /// <summary>
        /// TLS 协议版本，默认 Tls12（服务端启动流程当前未读取该值）
        /// </summary>
        public SslProtocols SslProtocols { get; set; } = SslProtocols.Tls12;
        
        /// <summary>
        /// 是否要求客户端提供证书，默认关闭（服务端启动流程当前未读取该值）
        /// </summary>
        public bool RequireClientCertificate { get; set; } = false;
        
        /// <summary>
        /// 校验 TLS 参数，启用时证书实例与证书路径至少提供一个
        /// </summary>
        public void Validate()
        {
            if (Enabled)
            {
                if (Certificate == null && string.IsNullOrWhiteSpace(CertificatePath))
                {
                    throw new P2PException(ErrorCode.AuthFailed, "Certificate is required when TLS is enabled");
                }
            }
        }
    }
    
    /// <summary>
    /// 服务端各类超时参数，单位均为毫秒
    /// </summary>
    public class ServerTimeoutOptions
    {
        /// <summary>
        /// 接受连接超时，默认 5000，必须大于 0（服务端当前未读取该值）
        /// </summary>
        public int AcceptTimeoutMs { get; set; } = 5000;
        
        /// <summary>
        /// 接收数据超时，默认 30000，作为底层 socket 的动作超时
        /// </summary>
        public int ReceiveTimeoutMs { get; set; } = 30000;
        
        /// <summary>
        /// 发送数据超时，默认 10000（服务端当前未读取该值）
        /// </summary>
        public int SendTimeoutMs { get; set; } = 10000;
        
        /// <summary>
        /// 心跳发送间隔，默认 30000（服务端当前未读取该值）
        /// </summary>
        public int HeartbeatIntervalMs { get; set; } = 30000;
        
        /// <summary>
        /// 心跳无响应判定超时，默认 10000（服务端当前未读取该值）
        /// </summary>
        public int HeartbeatTimeoutMs { get; set; } = 10000;
        
        /// <summary>
        /// 会话超时，默认 120000，必须大于 0，作为信令会话的过期依据
        /// </summary>
        public int SessionTimeoutMs { get; set; } = 120000;
        
        /// <summary>
        /// 注册等待超时，默认 10000（服务端当前未读取该值）
        /// </summary>
        public int RegisterTimeoutMs { get; set; } = 10000;
        
        /// <summary>
        /// 连接空闲回收时间，默认 120000，传给底层 socket 的空闲超时
        /// </summary>
        public int FreeTimeMs { get; set; } = 120000;
        
        /// <summary>
        /// 校验超时参数，接受连接超时与会话超时必须大于 0
        /// </summary>
        public void Validate()
        {
            if (AcceptTimeoutMs <= 0)
            {
                throw new P2PException(ErrorCode.RegisterTimeout, "AcceptTimeoutMs must be greater than 0");
            }
            
            if (SessionTimeoutMs <= 0)
            {
                throw new P2PException(ErrorCode.RegisterTimeout, "SessionTimeoutMs must be greater than 0");
            }
        }
    }
}