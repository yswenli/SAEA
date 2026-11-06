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
*文件名： P2PClientBuilder
*版本号： v26.4.23.1
*唯一标识：c23179e6-de17-4ecc-8dc6-469df898dd77
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 15:49:38
*描述：P2PClientBuilder接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:20:16
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Net;
using SAEA.P2P.Common;
using SAEA.P2P.NAT;

namespace SAEA.P2P.Builder
{
    /// <summary>
    /// P2P 客户端配置构建器，链式设置信令服务器、节点标识、打洞、中继、发现、加密与日志参数
    /// </summary>
    public class P2PClientBuilder
    {
        private readonly P2POptions _options = new P2POptions();

        /// <summary>
        /// 设置信令服务器地址与端口，任一项非法即抛出注册服务器不可用异常
        /// </summary>
        /// <param name="address">服务器 IP 或主机名，不能为空</param>
        /// <param name="port">服务器端口，取值 1-65535</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder SetServer(string address, int port)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "Server address cannot be empty");
            }
            if (port <= 0 || port > 65535)
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "Server port must be between 1 and 65535");
            }
            _options.ServerAddress = address;
            _options.ServerPort = port;
            return this;
        }

        /// <summary>
        /// 用终结点设置信令服务器地址与端口
        /// </summary>
        /// <param name="endpoint">服务器终结点，不能为 null</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder SetServer(IPEndPoint endpoint)
        {
            if (endpoint == null)
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "Endpoint cannot be null");
            }
            _options.ServerAddress = endpoint.Address.ToString();
            _options.ServerPort = endpoint.Port;
            return this;
        }

        /// <summary>
        /// 设置本节点标识，注册与局域网发现均使用该标识
        /// </summary>
        /// <param name="nodeId">节点标识，不能为空且长度不超过 64 个字符</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder SetNodeId(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                throw new P2PException(ErrorCode.RegisterPeerIdMissing, "NodeId cannot be empty");
            }
            if (nodeId.Length > 64)
            {
                throw new P2PException(ErrorCode.RegisterPeerIdInvalid, "NodeId length cannot exceed 64 characters");
            }
            _options.NodeId = nodeId;
            return this;
        }

        /// <summary>
        /// 设置节点鉴权密码，非空时注册阶段启用挑战应答认证
        /// </summary>
        /// <param name="password">鉴权密码，null 视为空字符串即不启用认证</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder SetNodeIdPassword(string password)
        {
            _options.NodeIdPassword = password ?? string.Empty;
            return this;
        }

        /// <summary>
        /// 设置连接信令服务器的超时时间
        /// </summary>
        /// <param name="timeoutMs">连接超时毫秒数，必须大于 0；仅影响 ConnectTimeoutMs，不影响收发超时</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder SetTimeout(int timeoutMs)
        {
            if (timeoutMs <= 0)
            {
                throw new P2PException(ErrorCode.RegisterTimeout, "Timeout must be greater than 0");
            }
            _options.Timeout.ConnectTimeoutMs = timeoutMs;
            return this;
        }

        /// <summary>
        /// 启用 NAT 打洞，沿用默认 PreferDirect 策略
        /// </summary>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder EnableHolePunch()
        {
            _options.HolePunch.Enabled = true;
            return this;
        }

        /// <summary>
        /// 启用 NAT 打洞并指定打洞策略
        /// </summary>
        /// <param name="strategy">打洞策略，RelayOnly 时不做直连尝试直接走中继</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder EnableHolePunch(HolePunchStrategy strategy)
        {
            _options.HolePunch.Enabled = true;
            _options.HolePunch.Strategy = strategy;
            return this;
        }

        /// <summary>
        /// 设置打洞同步等待时限
        /// </summary>
        /// <param name="timeoutMs">打洞总时限毫秒数，必须大于 0，超过即判定打洞失败</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder SetHolePunchTimeout(int timeoutMs)
        {
            if (timeoutMs <= 0)
            {
                throw new P2PException(ErrorCode.HolePunchTimeout, "HolePunch timeout must be greater than 0");
            }
            _options.HolePunch.SyncTimeoutMs = timeoutMs;
            return this;
        }

        /// <summary>
        /// 设置打洞最大尝试次数
        /// </summary>
        /// <param name="maxAttempts">尝试次数上限，必须大于 0；两次尝试之间固定间隔 100 毫秒</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder SetHolePunchRetry(int maxAttempts)
        {
            if (maxAttempts <= 0)
            {
                throw new P2PException(ErrorCode.HolePunchFailed, "HolePunch retry attempts must be greater than 0");
            }
            _options.HolePunch.MaxAttempts = maxAttempts;
            return this;
        }

        /// <summary>
        /// 设置本端 NAT 类型，供打洞前的兼容性判断记录
        /// </summary>
        /// <param name="natType">NAT 类型；打洞器实际使用服务端探测到的结果，改值不会覆盖探测结果</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder SetNATType(NATType natType)
        {
            _options.HolePunch.NATType = natType;
            return this;
        }

        /// <summary>
        /// 启用中继转发（默认已启用），沿用默认超时与不限量配额
        /// </summary>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder EnableRelay()
        {
            _options.Relay.Enabled = true;
            return this;
        }

        /// <summary>
        /// 启用中继转发并指定会话超时与字节配额
        /// </summary>
        /// <param name="timeout">中继会话空闲超时毫秒数，必须大于 0</param>
        /// <param name="quota">单会话累计转发字节配额，小于等于 0 表示不限</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder EnableRelay(int timeout, long quota)
        {
            if (timeout <= 0)
            {
                throw new P2PException(ErrorCode.RelayFailed, "Relay timeout must be greater than 0");
            }
            _options.Relay.Enabled = true;
            _options.Relay.TimeoutMs = timeout;
            _options.Relay.Quota = quota;
            return this;
        }

        /// <summary>
        /// 启用局域网组播发现（默认已启用），沿用默认端口 39655 与组播地址 239.255.255.250
        /// </summary>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder EnableLocalDiscovery()
        {
            _options.Discovery.EnableLocalDiscovery = true;
            return this;
        }

        /// <summary>
        /// 启用局域网组播发现并指定本地监听端口
        /// </summary>
        /// <param name="port">发现用 UDP 端口，取值 1-65535</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder EnableLocalDiscovery(int port)
        {
            if (port <= 0 || port > 65535)
            {
                throw new P2PException(ErrorCode.DiscoveryPortBlocked, "Discovery port must be between 1 and 65535");
            }
            _options.Discovery.EnableLocalDiscovery = true;
            _options.Discovery.LocalDiscoveryPort = port;
            return this;
        }

        /// <summary>
        /// 启用局域网组播发现并指定端口与组播地址
        /// </summary>
        /// <param name="port">发现用 UDP 端口，取值 1-65535</param>
        /// <param name="multicast">组播地址，不能为空，如 239.255.255.250</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder EnableLocalDiscovery(int port, string multicast)
        {
            if (port <= 0 || port > 65535)
            {
                throw new P2PException(ErrorCode.DiscoveryPortBlocked, "Discovery port must be between 1 and 65535");
            }
            if (string.IsNullOrWhiteSpace(multicast))
            {
                throw new P2PException(ErrorCode.DiscoveryAddressInvalid, "Multicast address cannot be empty");
            }
            _options.Discovery.EnableLocalDiscovery = true;
            _options.Discovery.LocalDiscoveryPort = port;
            _options.Discovery.MulticastAddress = multicast;
            return this;
        }

        /// <summary>
        /// 设置局域网发现的组播广播间隔
        /// </summary>
        /// <param name="intervalMs">广播间隔毫秒数，必须大于 0</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder SetDiscoveryInterval(int intervalMs)
        {
            if (intervalMs <= 0)
            {
                throw new P2PException(ErrorCode.DiscoveryFailed, "Discovery interval must be greater than 0");
            }
            _options.Discovery.DiscoveryIntervalMs = intervalMs;
            return this;
        }

        /// <summary>
        /// 启用对称加密，密钥需另由 EnableEncryption(string) 提供，构建时校验
        /// </summary>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder EnableEncryption()
        {
            _options.Encryption.Enabled = true;
            return this;
        }

        /// <summary>
        /// 启用对称加密并指定密钥
        /// </summary>
        /// <param name="key">密钥字符串，长度必须为 16、24 或 32 个字符，密钥位数按字符长度乘以 8 推导</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder EnableEncryption(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new P2PException(ErrorCode.EncryptionFailed, "Encryption key cannot be empty");
            }
            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
            {
                throw new P2PException(ErrorCode.EncryptionFailed, "Encryption key length must be 16, 24, or 32 characters");
            }
            _options.Encryption.Enabled = true;
            _options.Encryption.Key = key;
            _options.Encryption.KeySize = key.Length * 8;
            return this;
        }

        /// <summary>
        /// 打开 TLS 传输开关（连接流程当前未读取该值，实际加密由 EnableEncryption 的密钥完成）
        /// </summary>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder EnableTls()
        {
            _options.Encryption.TlsEnabled = true;
            return this;
        }

        /// <summary>
        /// 设置连接空闲回收时间（客户端当前未读取该值）
        /// </summary>
        /// <param name="freeTimeMs">空闲超时毫秒数，必须大于 0</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder SetFreeTime(int freeTimeMs)
        {
            if (freeTimeMs <= 0)
            {
                throw new P2PException(ErrorCode.RegisterTimeout, "FreeTime must be greater than 0");
            }
            _options.Timeout.FreeTimeMs = freeTimeMs;
            return this;
        }

        /// <summary>
        /// 设置对端心跳间隔（客户端当前未读取该值）
        /// </summary>
        /// <param name="heartbeatMs">心跳间隔毫秒数，必须大于 0</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder SetPeerHeartbeat(int heartbeatMs)
        {
            if (heartbeatMs <= 0)
            {
                throw new P2PException(ErrorCode.RegisterTimeout, "PeerHeartbeat must be greater than 0");
            }
            _options.Timeout.HeartbeatIntervalMs = heartbeatMs;
            return this;
        }

        /// <summary>
        /// 启用日志输出（默认已启用），沿用默认等级阈值 2（Info）
        /// </summary>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder EnableLogging()
        {
            _options.Logging.Enabled = true;
            return this;
        }

        /// <summary>
        /// 启用日志输出并设置等级阈值
        /// </summary>
        /// <param name="level">等级阈值：0=Trace、1=Debug、2=Info、3=Warning、4=Error，数值越大输出越少</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder EnableLogging(int level)
        {
            _options.Logging.Enabled = true;
            _options.Logging.Level = level;
            return this;
        }

        /// <summary>
        /// 启用日志输出并写入指定文件
        /// </summary>
        /// <param name="level">等级阈值：0=Trace、1=Debug、2=Info、3=Warning、4=Error，数值越大输出越少</param>
        /// <param name="path">日志文件路径，不能为空</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder EnableLogging(int level, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "Log file path cannot be empty");
            }
            _options.Logging.Enabled = true;
            _options.Logging.Level = level;
            _options.Logging.LogFilePath = path;
            _options.Logging.LogToFile = true;
            return this;
        }

        /// <summary>
        /// 设置是否输出到控制台（客户端当前未读取该值）
        /// </summary>
        /// <param name="enable">true 表示输出到控制台</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder SetLogToConsole(bool enable)
        {
            _options.Logging.LogToConsole = enable;
            return this;
        }

        /// <summary>
        /// 校验当前配置并返回选项实例，校验失败抛 P2PException
        /// </summary>
        /// <returns>与构建器共用同一 P2POptions 对象</returns>
        public P2POptions Build()
        {
            _options.Validate();
            return _options;
        }

        /// <summary>
        /// 获取当前累积的选项实例，不做校验
        /// </summary>
        /// <returns>与构建器共用同一 P2POptions 对象，后续链式改动会同步体现</returns>
        public P2POptions GetOptions()
        {
            return _options;
        }
    }
}