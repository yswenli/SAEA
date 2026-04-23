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
*唯一标识：a163ea4e-1196-4247-8395-8d5f6131ee46
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/4/20 15:49:38
*描述：P2P客户端构建器，提供链式配置客户端选项
*
*=====================================================================
*修改标记
*修改时间：2026/4/20 15:49:38
*修改人： yswenli
*版本号： v26.4.23.1
*描述：P2P客户端构建器，提供链式配置客户端选项
*
*****************************************************************************/
using System.Net;
using SAEA.P2P.Common;
using SAEA.P2P.NAT;

namespace SAEA.P2P.Builder
{
    public class P2PClientBuilder
    {
        private readonly P2POptions _options = new P2POptions();

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

        public P2PClientBuilder SetNodeIdPassword(string password)
        {
            _options.NodeIdPassword = password ?? string.Empty;
            return this;
        }

        public P2PClientBuilder SetTimeout(int timeoutMs)
        {
            if (timeoutMs <= 0)
            {
                throw new P2PException(ErrorCode.RegisterTimeout, "Timeout must be greater than 0");
            }
            _options.Timeout.ConnectTimeoutMs = timeoutMs;
            return this;
        }

        public P2PClientBuilder EnableHolePunch()
        {
            _options.HolePunch.Enabled = true;
            return this;
        }

        public P2PClientBuilder EnableHolePunch(HolePunchStrategy strategy)
        {
            _options.HolePunch.Enabled = true;
            _options.HolePunch.Strategy = strategy;
            return this;
        }

        public P2PClientBuilder SetHolePunchTimeout(int timeoutMs)
        {
            if (timeoutMs <= 0)
            {
                throw new P2PException(ErrorCode.HolePunchTimeout, "HolePunch timeout must be greater than 0");
            }
            _options.HolePunch.SyncTimeoutMs = timeoutMs;
            return this;
        }

        public P2PClientBuilder SetHolePunchRetry(int maxAttempts)
        {
            if (maxAttempts <= 0)
            {
                throw new P2PException(ErrorCode.HolePunchFailed, "HolePunch retry attempts must be greater than 0");
            }
            _options.HolePunch.MaxAttempts = maxAttempts;
            return this;
        }

        public P2PClientBuilder SetNATType(NATType natType)
        {
            _options.HolePunch.NATType = natType;
            return this;
        }

        public P2PClientBuilder EnableRelay()
        {
            _options.Relay.Enabled = true;
            return this;
        }

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

        public P2PClientBuilder EnableLocalDiscovery()
        {
            _options.Discovery.EnableLocalDiscovery = true;
            return this;
        }

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

        public P2PClientBuilder SetDiscoveryInterval(int intervalMs)
        {
            if (intervalMs <= 0)
            {
                throw new P2PException(ErrorCode.DiscoveryFailed, "Discovery interval must be greater than 0");
            }
            _options.Discovery.DiscoveryIntervalMs = intervalMs;
            return this;
        }

        public P2PClientBuilder EnableEncryption()
        {
            _options.Encryption.Enabled = true;
            return this;
        }

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

        public P2PClientBuilder EnableTls()
        {
            _options.Encryption.TlsEnabled = true;
            return this;
        }

        public P2PClientBuilder SetFreeTime(int freeTimeMs)
        {
            if (freeTimeMs <= 0)
            {
                throw new P2PException(ErrorCode.RegisterTimeout, "FreeTime must be greater than 0");
            }
            _options.Timeout.FreeTimeMs = freeTimeMs;
            return this;
        }

        public P2PClientBuilder SetPeerHeartbeat(int heartbeatMs)
        {
            if (heartbeatMs <= 0)
            {
                throw new P2PException(ErrorCode.RegisterTimeout, "PeerHeartbeat must be greater than 0");
            }
            _options.Timeout.HeartbeatIntervalMs = heartbeatMs;
            return this;
        }

        public P2PClientBuilder EnableLogging()
        {
            _options.Logging.Enabled = true;
            return this;
        }

        public P2PClientBuilder EnableLogging(int level)
        {
            _options.Logging.Enabled = true;
            _options.Logging.Level = level;
            return this;
        }

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

        public P2PClientBuilder SetLogToConsole(bool enable)
        {
            _options.Logging.LogToConsole = enable;
            return this;
        }

        public P2POptions Build()
        {
            _options.Validate();
            return _options;
        }

        public P2POptions GetOptions()
        {
            return _options;
        }
    }
}