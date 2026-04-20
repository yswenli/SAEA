using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using SAEA.P2P.Common;

namespace SAEA.P2P.Builder
{
    public class P2PServerOptions
    {
        public string BindIP { get; set; } = "0.0.0.0";
        
        public int Port { get; set; } = 39654;
        
        public int MaxNodes { get; set; } = 10000;
        
        public ServerRelayOptions Relay { get; set; } = new ServerRelayOptions();
        
        public ServerTlsOptions Tls { get; set; } = new ServerTlsOptions();
        
        public ServerTimeoutOptions Timeout { get; set; } = new ServerTimeoutOptions();
        
        public LoggingOptions Logging { get; set; } = new LoggingOptions();
        
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
            
            Relay?.Validate();
            Tls?.Validate();
            Timeout?.Validate();
        }
    }
    
    public class ServerRelayOptions
    {
        public bool Enabled { get; set; } = true;
        
        public int MaxRelaySessions { get; set; } = 1000;
        
        public int RelayBufferSize { get; set; } = 64 * 1024;
        
        public int MaxRelayBandwidth { get; set; } = 10 * 1024 * 1024;
        
        public long MaxQuota { get; set; } = 0;
        
        public void Validate()
        {
            if (MaxRelaySessions <= 0)
            {
                throw new P2PException(ErrorCode.RelayFailed, "MaxRelaySessions must be greater than 0");
            }
            
            if (RelayBufferSize <= 0)
            {
                throw new P2PException(ErrorCode.RelayFailed, "RelayBufferSize must be greater than 0");
            }
        }
    }
    
    public class ServerTlsOptions
    {
        public bool Enabled { get; set; } = false;
        
        public string CertificatePath { get; set; }
        
        public string CertificatePassword { get; set; }
        
        public X509Certificate2 Certificate { get; set; }
        
        public SslProtocols SslProtocols { get; set; } = SslProtocols.Tls12;
        
        public bool RequireClientCertificate { get; set; } = false;
        
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
    
    public class ServerTimeoutOptions
    {
        public int AcceptTimeoutMs { get; set; } = 5000;
        
        public int ReceiveTimeoutMs { get; set; } = 30000;
        
        public int SendTimeoutMs { get; set; } = 10000;
        
        public int HeartbeatIntervalMs { get; set; } = 30000;
        
        public int HeartbeatTimeoutMs { get; set; } = 10000;
        
        public int SessionTimeoutMs { get; set; } = 120000;
        
        public int RegisterTimeoutMs { get; set; } = 10000;
        
        public int FreeTimeMs { get; set; } = 120000;
        
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