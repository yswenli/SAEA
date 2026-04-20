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