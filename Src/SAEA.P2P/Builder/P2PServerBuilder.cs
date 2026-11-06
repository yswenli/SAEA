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
*修改时间：2026/11/6 12:20:16
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.P2P.Common;

namespace SAEA.P2P.Builder
{
    /// <summary>
    /// P2P 信令服务端配置构建器，链式设置绑定地址、端口、容量、中继、TLS 与日志参数
    /// </summary>
    public class P2PServerBuilder
    {
        private readonly P2PServerOptions _options = new P2PServerOptions();

        /// <summary>
        /// 设置服务端监听端口
        /// </summary>
        /// <param name="port">监听端口，取值 1-65535</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PServerBuilder SetPort(int port)
        {
            if (port <= 0 || port > 65535)
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "Port must be between 1 and 65535");
            }
            _options.Port = port;
            return this;
        }

        /// <summary>
        /// 设置服务端绑定地址
        /// </summary>
        /// <param name="ip">绑定 IP，不能为空，0.0.0.0 表示监听全部网卡</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PServerBuilder SetIP(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "IP address cannot be empty");
            }
            _options.BindIP = ip;
            return this;
        }

        /// <summary>
        /// 设置最大在线节点数
        /// </summary>
        /// <param name="maxNodes">最大连接数上限，必须大于 0</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PServerBuilder SetMaxNodes(int maxNodes)
        {
            if (maxNodes <= 0)
            {
                throw new P2PException(ErrorCode.RegisterServerUnavailable, "MaxNodes must be greater than 0");
            }
            _options.MaxNodes = maxNodes;
            return this;
        }

        /// <summary>
        /// 启用中继（默认已启用），沿用默认会话上限与不限量字节配额
        /// </summary>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PServerBuilder EnableRelay()
        {
            _options.Relay.Enabled = true;
            return this;
        }

        /// <summary>
        /// 启用中继并指定会话上限与字节配额
        /// </summary>
        /// <param name="maxConnections">并发中继会话上限，必须大于 0</param>
        /// <param name="maxQuota">单会话累计转发字节配额，小于等于 0 表示不限</param>
        /// <returns>当前构建器，便于链式调用</returns>
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

        /// <summary>
        /// 启用 TLS 并指定服务端证书文件
        /// </summary>
        /// <param name="certPath">证书文件路径，不能为空</param>
        /// <param name="password">证书密码，null 视为空字符串</param>
        /// <returns>当前构建器，便于链式调用</returns>
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

        /// <summary>
        /// 设置连接空闲回收时间
        /// </summary>
        /// <param name="freeTimeMs">空闲超时毫秒数，必须大于 0，作为底层 socket 的空闲超时</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PServerBuilder SetFreeTime(int freeTimeMs)
        {
            if (freeTimeMs <= 0)
            {
                throw new P2PException(ErrorCode.RegisterTimeout, "FreeTime must be greater than 0");
            }
            _options.Timeout.FreeTimeMs = freeTimeMs;
            return this;
        }

        /// <summary>
        /// 启用日志输出（默认已启用），沿用默认等级阈值 2（Info）
        /// </summary>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PServerBuilder EnableLogging()
        {
            _options.Logging.Enabled = true;
            return this;
        }

        /// <summary>
        /// 启用日志输出并设置等级阈值
        /// </summary>
        /// <param name="level">等级阈值：0=Trace、1=Debug、2=Info、3=Warning、4=Error，数值越大输出越少</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PServerBuilder EnableLogging(int level)
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

        /// <summary>
        /// 校验当前配置并返回选项实例，校验失败抛 P2PException
        /// </summary>
        /// <returns>与构建器共用同一 P2PServerOptions 对象</returns>
        public P2PServerOptions Build()
        {
            _options.Validate();
            return _options;
        }
    }
}