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
*修改时间：2026/04/20 15:44:01
*修改人： yswenli
*版本号： v26.4.23.1
*描述：P2POptions接口
*
*****************************************************************************/
using System;
using SAEA.P2P.Common;
using SAEA.P2P.NAT;

namespace SAEA.P2P.Builder
{
    public class P2POptions
    {
        public string ServerAddress { get; set; } = "127.0.0.1";
        
        public int ServerPort { get; set; } = 39654;
        
        public string NodeId { get; set; }
        
        public string NodeIdPassword { get; set; }
        
        public HolePunchOptions HolePunch { get; set; } = new HolePunchOptions();
        
        public RelayOptions Relay { get; set; } = new RelayOptions();
        
        public DiscoveryOptions Discovery { get; set; } = new DiscoveryOptions();
        
        public EncryptionOptions Encryption { get; set; } = new EncryptionOptions();
        
        public TimeoutOptions Timeout { get; set; } = new TimeoutOptions();
        
        public LoggingOptions Logging { get; set; } = new LoggingOptions();
        
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ServerAddress))
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "Server address is required");
            }
            
            if (ServerPort <= 0 || ServerPort > 65535)
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
    
    public class HolePunchOptions
    {
        public bool Enabled { get; set; } = false;
        
        public HolePunchStrategy Strategy { get; set; } = HolePunchStrategy.PreferDirect;
        
        public int MaxAttempts { get; set; } = 5;
        
        public int AttemptIntervalMs { get; set; } = 100;
        
        public int SyncTimeoutMs { get; set; } = 5000;
        
        public NATType NATType { get; set; } = NATType.Unknown;
        
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
    
    public class RelayOptions
    {
        public bool Enabled { get; set; } = true;
        
        public int MaxRelayConnections { get; set; } = 100;
        
        public int RelayBufferSize { get; set; } = 64 * 1024;
        
        public int TimeoutMs { get; set; } = 30000;
        
        public long Quota { get; set; } = 0;
        
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
    
    public class DiscoveryOptions
    {
        public bool EnableLocalDiscovery { get; set; } = true;
        
        public int LocalDiscoveryPort { get; set; } = 39655;
        
        public string MulticastAddress { get; set; } = "239.255.255.250";
        
        public int DiscoveryIntervalMs { get; set; } = 5000;
        
        public int DiscoveryTimeoutMs { get; set; } = 3000;
        
        public int MaxDiscoveryAttempts { get; set; } = 3;
        
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
    
    public class EncryptionOptions
    {
        public bool Enabled { get; set; } = false;
        
        public string Algorithm { get; set; } = "AES-256-GCM";
        
        public int KeySize { get; set; } = 256;
        
        public string Key { get; set; }
        
        public bool TlsEnabled { get; set; } = false;
        
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
            }
        }
    }
    
    public class TimeoutOptions
    {
        public int ConnectTimeoutMs { get; set; } = 10000;
        
        public int SendTimeoutMs { get; set; } = 5000;
        
        public int ReceiveTimeoutMs { get; set; } = 5000;
        
        public int HeartbeatIntervalMs { get; set; } = 30000;
        
        public int HeartbeatTimeoutMs { get; set; } = 10000;
        
        public int SessionTimeoutMs { get; set; } = 60000;
        
        public int FreeTimeMs { get; set; } = 60000;
        
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
    
    public class LoggingOptions
    {
        public bool Enabled { get; set; } = true;
        
        public int Level { get; set; } = 2;
        
        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;
        
        public bool LogToFile { get; set; } = false;
        
        public string LogFilePath { get; set; } = "logs/p2p.log";
        
        public bool LogToConsole { get; set; } = true;
    }
    
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warning = 3,
        Error = 4
    }
}