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
*文件名： P2POptions
*版本号： v26.4.23.1
*唯一标识：198e83a4-e76c-4f1d-a673-1b72ae81fab7
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 15:44:01
*描述：P2POptions接口
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
using SAEA.P2P.Common;
using SAEA.P2P.NAT;

namespace SAEA.P2P.Builder
{
    /// <summary>
    /// P2P 客户端总配置，聚合信令服务器、节点标识、打洞、中继、发现、加密、超时与日志子配置
    /// </summary>
    public class P2POptions
    {
        /// <summary>
        /// 信令服务器地址，为空时客户端进入仅局域网发现模式
        /// </summary>
        public string ServerAddress { get; set; }
        
        /// <summary>
        /// 信令服务器端口，默认 39654，仅在配置了服务器地址时校验 1-65535
        /// </summary>
        public int ServerPort { get; set; } = 39654;
        
        /// <summary>
        /// 本节点标识，必填，注册与局域网发现都用它标识节点
        /// </summary>
        public string NodeId { get; set; }
        
        /// <summary>
        /// 节点鉴权密码，为空表示注册时不启用挑战应答认证
        /// </summary>
        public string NodeIdPassword { get; set; }
        
        /// <summary>
        /// NAT 打洞配置
        /// </summary>
        public HolePunchOptions HolePunch { get; set; } = new HolePunchOptions();
        
        /// <summary>
        /// 中继转发配置
        /// </summary>
        public RelayOptions Relay { get; set; } = new RelayOptions();
        
        /// <summary>
        /// 局域网组播发现配置
        /// </summary>
        public DiscoveryOptions Discovery { get; set; } = new DiscoveryOptions();
        
        /// <summary>
        /// 对称加密配置
        /// </summary>
        public EncryptionOptions Encryption { get; set; } = new EncryptionOptions();
        
        /// <summary>
        /// 各类超时配置
        /// </summary>
        public TimeoutOptions Timeout { get; set; } = new TimeoutOptions();
        
        /// <summary>
        /// 日志输出配置
        /// </summary>
        public LoggingOptions Logging { get; set; } = new LoggingOptions();
        
        /// <summary>
        /// 校验必填项与各子配置，未配置信令服务器时跳过端口校验以支持仅局域网模式
        /// </summary>
        public void Validate()
        {
            // Local-only mode (no signal server) is supported: when ServerAddress is
            // empty the client relies solely on local discovery. Only validate the
            // port when a signal server is actually configured.
            if (!string.IsNullOrWhiteSpace(ServerAddress) && (ServerPort <= 0 || ServerPort > 65535))
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "Server port must be between 1 and 65535");
            }
            
            if (string.IsNullOrWhiteSpace(NodeId))
            {
                throw new P2PException(ErrorCode.RegisterPeerIdMissing, "Node ID is required");
            }
            
            HolePunch?.Validate();
            Relay?.Validate();
            Discovery?.Validate();
            Encryption?.Validate();
            Timeout?.Validate();
        }
    }
    
    /// <summary>
    /// NAT 打洞参数，控制启用开关、打洞策略、尝试次数与同步超时
    /// </summary>
    public class HolePunchOptions
    {
        /// <summary>
        /// 是否启用打洞，默认关闭
        /// </summary>
        public bool Enabled { get; set; } = false;
        
        /// <summary>
        /// 打洞策略，默认优先直连 PreferDirect
        /// </summary>
        public HolePunchStrategy Strategy { get; set; } = HolePunchStrategy.PreferDirect;
        
        /// <summary>
        /// 打洞最大尝试次数，默认 5，必须大于 0
        /// </summary>
        public int MaxAttempts { get; set; } = 5;
        
        /// <summary>
        /// 打洞尝试间隔毫秒数，默认 100，必须大于 0（当前打洞器固定按 100 毫秒间隔，不读取该值）
        /// </summary>
        public int AttemptIntervalMs { get; set; } = 100;
        
        /// <summary>
        /// 打洞总时限毫秒数，默认 5000，超过该时限即判定打洞失败
        /// </summary>
        public int SyncTimeoutMs { get; set; } = 5000;
        
        /// <summary>
        /// 预置的本端 NAT 类型，默认 Unknown（打洞器实际使用服务端探测结果）
        /// </summary>
        public NATType NATType { get; set; } = NATType.Unknown;
        
        /// <summary>
        /// 校验打洞参数，最大尝试次数与尝试间隔必须大于 0
        /// </summary>
        public void Validate()
        {
            if (MaxAttempts <= 0)
            {
                throw new P2PException(ErrorCode.HolePunchFailed, "MaxAttempts must be greater than 0");
            }
            
            if (AttemptIntervalMs <= 0)
            {
                throw new P2PException(ErrorCode.HolePunchFailed, "AttemptIntervalMs must be greater than 0");
            }
        }
    }
    
    /// <summary>
    /// 中继转发参数，控制启用开关、连接上限、缓冲区与字节配额
    /// </summary>
    public class RelayOptions
    {
        /// <summary>
        /// 是否启用中继，默认启用
        /// </summary>
        public bool Enabled { get; set; } = true;
        
        /// <summary>
        /// 中继连接上限，默认 100，必须大于 0（当前运行时未读取该值）
        /// </summary>
        public int MaxRelayConnections { get; set; } = 100;
        
        /// <summary>
        /// 中继转发缓冲区字节数，默认 64 KB，必须大于 0（当前运行时未读取该值）
        /// </summary>
        public int RelayBufferSize { get; set; } = 64 * 1024;
        
        /// <summary>
        /// 中继会话空闲超时毫秒数，默认 30000，作为中继管理器的过期判定依据
        /// </summary>
        public int TimeoutMs { get; set; } = 30000;
        
        /// <summary>
        /// 单中继会话累计转发字节配额，默认 0 表示不限
        /// </summary>
        public long Quota { get; set; } = 0;
        
        /// <summary>
        /// 校验中继参数，连接上限与缓冲区大小必须大于 0
        /// </summary>
        public void Validate()
        {
            if (MaxRelayConnections <= 0)
            {
                throw new P2PException(ErrorCode.RelayFailed, "MaxRelayConnections must be greater than 0");
            }
            
            if (RelayBufferSize <= 0)
            {
                throw new P2PException(ErrorCode.RelayFailed, "RelayBufferSize must be greater than 0");
            }
        }
    }
    
    /// <summary>
    /// 局域网组播发现参数，控制发现开关、端口、组播地址与广播节奏
    /// </summary>
    public class DiscoveryOptions
    {
        /// <summary>
        /// 是否启用局域网组播发现，默认启用
        /// </summary>
        public bool EnableLocalDiscovery { get; set; } = true;
        
        /// <summary>
        /// 本地发现 UDP 端口，默认 39655，取值 1-65535
        /// </summary>
        public int LocalDiscoveryPort { get; set; } = 39655;
        
        /// <summary>
        /// 发现使用的组播地址，默认 239.255.255.250，为空时不加入组播组
        /// </summary>
        public string MulticastAddress { get; set; } = "239.255.255.250";
        
        /// <summary>
        /// 组播广播间隔毫秒数，默认 5000
        /// </summary>
        public int DiscoveryIntervalMs { get; set; } = 5000;
        
        /// <summary>
        /// 已发现节点的过期判定毫秒数，默认 3000，必须大于 0
        /// </summary>
        public int DiscoveryTimeoutMs { get; set; } = 3000;
        
        /// <summary>
        /// 发现重试次数上限，默认 3（当前运行时未读取该值）
        /// </summary>
        public int MaxDiscoveryAttempts { get; set; } = 3;
        
        /// <summary>
        /// 校验发现参数，本地端口需在 1-65535 且节点过期阈值必须大于 0
        /// </summary>
        public void Validate()
        {
            if (LocalDiscoveryPort <= 0 || LocalDiscoveryPort > 65535)
            {
                throw new P2PException(ErrorCode.DiscoveryPortBlocked, "LocalDiscoveryPort must be between 1 and 65535");
            }
            
            if (DiscoveryTimeoutMs <= 0)
            {
                throw new P2PException(ErrorCode.DiscoveryTimeout, "DiscoveryTimeoutMs must be greater than 0");
            }
        }
    }
    
    /// <summary>
    /// 对称加密参数，控制启用开关、算法、密钥长度与密钥值
    /// </summary>
    public class EncryptionOptions
    {
        /// <summary>
        /// 是否启用对称加密，默认关闭；启用时构建阶段要求密钥非空
        /// </summary>
        public bool Enabled { get; set; } = false;
        
        /// <summary>
        /// 加密算法名称，默认 AES-256-GCM（当前加密服务只使用密钥，不读取该值）
        /// </summary>
        public string Algorithm { get; set; } = "AES-256-GCM";
        
        /// <summary>
        /// 密钥位数，默认 256，启用时只能取 128、192 或 256（实际加解密按密钥长度推导）
        /// </summary>
        public int KeySize { get; set; } = 256;
        
        /// <summary>
        /// 对称密钥字符串，启用加密时必填
        /// </summary>
        public string Key { get; set; }
        
        /// <summary>
        /// 是否启用 TLS 通道，默认关闭（客户端连接流程当前未读取该值）
        /// </summary>
        public bool TlsEnabled { get; set; } = false;
        
        /// <summary>
        /// 校验加密参数，仅在启用加密时检查算法、密钥位数与密钥
        /// </summary>
        public void Validate()
        {
            if (Enabled)
            {
                if (string.IsNullOrWhiteSpace(Algorithm))
                {
                    throw new P2PException(ErrorCode.EncryptionFailed, "Algorithm is required when encryption is enabled");
                }
                
                if (KeySize != 128 && KeySize != 192 && KeySize != 256)
                {
                    throw new P2PException(ErrorCode.EncryptionFailed, "KeySize must be 128, 192, or 256");
                }
                
                if (string.IsNullOrEmpty(Key))
                {
                    throw new P2PException(ErrorCode.EncryptionFailed, "Key is required when encryption is enabled");
                }
            }
        }
    }
    
    /// <summary>
    /// 客户端各类超时参数，除特别说明外单位均为毫秒
    /// </summary>
    public class TimeoutOptions
    {
        /// <summary>
        /// 连接信令服务器超时，默认 10000，必须大于 0
        /// </summary>
        public int ConnectTimeoutMs { get; set; } = 10000;
        
        /// <summary>
        /// 发送超时，默认 5000（客户端当前未读取该值）
        /// </summary>
        public int SendTimeoutMs { get; set; } = 5000;
        
        /// <summary>
        /// 接收超时，默认 5000（客户端当前未读取该值）
        /// </summary>
        public int ReceiveTimeoutMs { get; set; } = 5000;
        
        /// <summary>
        /// 心跳发送间隔，默认 30000，必须大于 0（心跳逻辑尚未接入客户端运行时）
        /// </summary>
        public int HeartbeatIntervalMs { get; set; } = 30000;
        
        /// <summary>
        /// 心跳无响应判定超时，默认 10000（客户端当前未读取该值）
        /// </summary>
        public int HeartbeatTimeoutMs { get; set; } = 10000;
        
        /// <summary>
        /// 会话超时，默认 60000（客户端当前未读取该值）
        /// </summary>
        public int SessionTimeoutMs { get; set; } = 60000;
        
        /// <summary>
        /// 连接空闲回收时间，默认 60000（客户端当前未读取该值）
        /// </summary>
        public int FreeTimeMs { get; set; } = 60000;
        
        /// <summary>
        /// 校验超时参数，连接超时与心跳间隔必须大于 0
        /// </summary>
        public void Validate()
        {
            if (ConnectTimeoutMs <= 0)
            {
                throw new P2PException(ErrorCode.RegisterTimeout, "ConnectTimeoutMs must be greater than 0");
            }
            
            if (HeartbeatIntervalMs <= 0)
            {
                throw new P2PException(ErrorCode.RegisterTimeout, "HeartbeatIntervalMs must be greater than 0");
            }
        }
    }
    
    /// <summary>
    /// 日志输出参数，控制开关、等级阈值与输出目标
    /// </summary>
    public class LoggingOptions
    {
        /// <summary>
        /// 是否启用日志，默认开启（当前运行时未读取该值，输出与否由等级阈值决定）
        /// </summary>
        public bool Enabled { get; set; } = true;
        
        /// <summary>
        /// 日志等级阈值，默认 2；0=Trace、1=Debug、2=Info、3=Warning、4=Error，数值越大输出越少
        /// </summary>
        public int Level { get; set; } = 2;
        
        /// <summary>
        /// 日志最低等级的枚举形式，默认 Info（运行时以 Level 数值为准，该值未被读取）
        /// </summary>
        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;
        
        /// <summary>
        /// 是否写入日志文件，默认关闭（当前运行时未读取该值）
        /// </summary>
        public bool LogToFile { get; set; } = false;
        
        /// <summary>
        /// 日志文件路径，默认 logs/p2p.log（当前运行时未读取该值）
        /// </summary>
        public string LogFilePath { get; set; } = "logs/p2p.log";
        
        /// <summary>
        /// 是否输出到控制台，默认开启（当前运行时未读取该值）
        /// </summary>
        public bool LogToConsole { get; set; } = true;
    }
    
    /// <summary>
    /// 日志等级枚举，数值与 LoggingOptions.Level 的阈值比较方向一致：数值越小越详细
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// 跟踪级，输出全部细节
        /// </summary>
        Trace = 0,
        /// <summary>
        /// 调试级，输出诊断信息
        /// </summary>
        Debug = 1,
        /// <summary>
        /// 信息级，输出常规运行信息
        /// </summary>
        Info = 2,
        /// <summary>
        /// 警告级，输出可恢复的异常情况
        /// </summary>
        Warning = 3,
        /// <summary>
        /// 错误级，输出失败与异常信息
        /// </summary>
        Error = 4
    }
}