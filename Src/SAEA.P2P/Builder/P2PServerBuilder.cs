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
*文件名： P2PServerBuilder
*版本号： v26.4.23.1
*唯一标识：9beca2ef-66d6-4e2e-a7df-fbe66eaa5667
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 15:49:38
*描述：P2PServerBuilder接口
*
*=====================================================================
*修改标记
*修改时间：2026/04/20 15:49:38
*修改人： yswenli
*版本号： v26.4.23.1
*描述：P2PServerBuilder接口
*
*****************************************************************************/
using SAEA.P2P.Common;

namespace SAEA.P2P.Builder
{
    public class P2PServerBuilder
    {
        private readonly P2PServerOptions _options = new P2PServerOptions();

        public P2PServerBuilder SetPort(int port)
        {
            if (port <= 0 || port > 65535)
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "Port must be between 1 and 65535");
            }
            _options.Port = port;
            return this;
        }

        public P2PServerBuilder SetIP(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "IP address cannot be empty");
            }
            _options.BindIP = ip;
            return this;
        }

        public P2PServerBuilder SetMaxNodes(int maxNodes)
        {
            if (maxNodes <= 0)
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "MaxNodes must be greater than 0");
            }
            _options.MaxNodes = maxNodes;
            return this;
        }

        public P2PServerBuilder EnableRelay()
        {
            _options.Relay.Enabled = true;
            return this;
        }

        public P2PServerBuilder EnableRelay(int maxConnections, long maxQuota)
        {
            if (maxConnections <= 0)
            {
                throw new P2PException(ErrorCode.RelayFailed, "MaxConnections must be greater than 0");
            }
            _options.Relay.Enabled = true;
            _options.Relay.MaxRelaySessions = maxConnections;
            _options.Relay.MaxQuota = maxQuota;
            return this;
        }

        public P2PServerBuilder EnableTls(string certPath, string password)
        {
            if (string.IsNullOrWhiteSpace(certPath))
            {
                throw new P2PException(ErrorCode.AuthFailed, "Certificate path cannot be empty");
            }
            _options.Tls.Enabled = true;
            _options.Tls.CertificatePath = certPath;
            _options.Tls.CertificatePassword = password ?? string.Empty;
            return this;
        }

        public P2PServerBuilder SetFreeTime(int freeTimeMs)
        {
            if (freeTimeMs <= 0)
            {
                throw new P2PException(ErrorCode.RegisterTimeout, "FreeTime must be greater than 0");
            }
            _options.Timeout.FreeTimeMs = freeTimeMs;
            return this;
        }

        public P2PServerBuilder EnableLogging()
        {
            _options.Logging.Enabled = true;
            return this;
        }

        public P2PServerBuilder EnableLogging(int level)
        {
            _options.Logging.Enabled = true;
            _options.Logging.Level = level;
            return this;
        }

        public P2PServerBuilder EnableLogging(int level, string path)
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

        public P2PServerOptions Build()
        {
            _options.Validate();
            return _options;
        }
    }
}