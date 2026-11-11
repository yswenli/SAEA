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
*命名空间：SAEA.P2P.TurnServer
*文件名： TurnServerOptions
*版本号： v26.11.5.1
*唯一标识：adb7677f-658c-4d8b-8124-ee6f6290453e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 00:20:00
*描述：TURN 服务端配置
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 03:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 ConnectionBindTimeout 与 RelayConnectTimeout 两项配置
*
*****************************************************************************/
using System;
using System.Net;

namespace SAEA.P2P.TurnServer
{
    /// <summary>
    /// TURN 服务端配置：共享密钥与认证域必填，中继地址必须显式给出，
    /// 其余项都有可用默认值。密钥与口令只从这里读取，不进入日志。
    /// </summary>
    public sealed class TurnServerOptions
    {
        /// <summary>
        /// 认证域
        /// </summary>
        private string _realm;

        /// <summary>
        /// 共享密钥
        /// </summary>
        private string _sharedSecret;

        /// <summary>
        /// 凭据有效期，单位秒
        /// </summary>
        private int _credentialTtlSeconds = 600;

        /// <summary>
        /// nonce 有效期，单位秒
        /// </summary>
        private int _nonceLifetimeSeconds = 600;

        /// <summary>
        /// 中继地址
        /// </summary>
        private IPAddress _relayAddress;

        /// <summary>
        /// 中继端口下界
        /// </summary>
        private int _relayPortMin = 49152;

        /// <summary>
        /// 中继端口上界
        /// </summary>
        private int _relayPortMax = 65535;

        /// <summary>
        /// 默认分配生命周期，单位秒
        /// </summary>
        private uint _defaultLifetimeSeconds = 600;

        /// <summary>
        /// 分配生命周期上限，单位秒
        /// </summary>
        private uint _maxLifetimeSeconds = 3600;

        /// <summary>
        /// 并发分配总数上限
        /// </summary>
        private int _maxAllocations = 1024;

        /// <summary>
        /// 单用户并发分配数上限
        /// </summary>
        private int _maxAllocationsPerUser = 8;

        /// <summary>
        /// 单个分配的累计中继字节上限，0 表示不限制
        /// </summary>
        private long _maxBytes;

        /// <summary>
        /// 数据连接等待 ConnectionBind 的超时时间
        /// </summary>
        private TimeSpan _connectionBindTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 中继连接建立超时时间
        /// </summary>
        private TimeSpan _relayConnectTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// SOFTWARE 属性值
        /// </summary>
        private string _software = "SAEA.P2P TURN Server";

        /// <summary>
        /// 时钟
        /// </summary>
        private ITurnClock _clock = SystemTurnClock.Instance;

        /// <summary>
        /// 认证域；必须与信令服务器签发凭据时使用的域一致
        /// </summary>
        public string Realm
        {
            get { return _realm; }
            set { _realm = value; }
        }

        /// <summary>
        /// 共享密钥；必须与信令服务器签发凭据时使用的密钥一致
        /// </summary>
        public string SharedSecret
        {
            get { return _sharedSecret; }
            set { _sharedSecret = value; }
        }

        /// <summary>
        /// 凭据有效期，单位秒
        /// </summary>
        public int CredentialTtlSeconds
        {
            get { return _credentialTtlSeconds; }
            set { _credentialTtlSeconds = value; }
        }

        /// <summary>
        /// nonce 有效期，单位秒
        /// </summary>
        public int NonceLifetimeSeconds
        {
            get { return _nonceLifetimeSeconds; }
            set { _nonceLifetimeSeconds = value; }
        }

        /// <summary>
        /// 中继地址；必须显式指定，且与监听地址族一致才可分配
        /// </summary>
        public IPAddress RelayAddress
        {
            get { return _relayAddress; }
            set { _relayAddress = value; }
        }

        /// <summary>
        /// 中继端口下界
        /// </summary>
        public int RelayPortMin
        {
            get { return _relayPortMin; }
            set { _relayPortMin = value; }
        }

        /// <summary>
        /// 中继端口上界
        /// </summary>
        public int RelayPortMax
        {
            get { return _relayPortMax; }
            set { _relayPortMax = value; }
        }

        /// <summary>
        /// 默认分配生命周期，单位秒
        /// </summary>
        public uint DefaultLifetimeSeconds
        {
            get { return _defaultLifetimeSeconds; }
            set { _defaultLifetimeSeconds = value; }
        }

        /// <summary>
        /// 分配生命周期上限，单位秒；客户端请求更长时按上限回填
        /// </summary>
        public uint MaxLifetimeSeconds
        {
            get { return _maxLifetimeSeconds; }
            set { _maxLifetimeSeconds = value; }
        }

        /// <summary>
        /// 并发分配总数上限，超过时回 486
        /// </summary>
        public int MaxAllocations
        {
            get { return _maxAllocations; }
            set { _maxAllocations = value; }
        }

        /// <summary>
        /// 单用户并发分配数上限，超过时回 486
        /// </summary>
        public int MaxAllocationsPerUser
        {
            get { return _maxAllocationsPerUser; }
            set { _maxAllocationsPerUser = value; }
        }

        /// <summary>
        /// 单个分配的累计中继字节上限，0 表示不限制；超出后数据被丢弃
        /// </summary>
        public long MaxBytes
        {
            get { return _maxBytes; }
            set { _maxBytes = value; }
        }

        /// <summary>
        /// 数据连接等待 ConnectionBind 的超时时间；超时后服务端关闭该条数据连接，默认 30 秒
        /// </summary>
        public TimeSpan ConnectionBindTimeout
        {
            get { return _connectionBindTimeout; }
            set { _connectionBindTimeout = value; }
        }

        /// <summary>
        /// 中继连接建立超时时间，默认 10 秒
        /// </summary>
        public TimeSpan RelayConnectTimeout
        {
            get { return _relayConnectTimeout; }
            set { _relayConnectTimeout = value; }
        }

        /// <summary>
        /// SOFTWARE 属性值；为空时不携带该属性
        /// </summary>
        public string Software
        {
            get { return _software; }
            set { _software = value; }
        }

        /// <summary>
        /// 时钟；测试可注入手工推进的实现
        /// </summary>
        public ITurnClock Clock
        {
            get { return _clock; }
            set { _clock = value ?? SystemTurnClock.Instance; }
        }

        /// <summary>
        /// 校验必填项与取值范围
        /// </summary>
        /// <exception cref="ArgumentException">共享密钥、认证域或中继地址缺失</exception>
        /// <exception cref="ArgumentOutOfRangeException">数值项越界</exception>
        internal void Validate()
        {
            if (string.IsNullOrEmpty(_sharedSecret))
            {
                throw new ArgumentException("必须配置共享密钥", nameof(SharedSecret));
            }
            if (string.IsNullOrEmpty(_realm))
            {
                throw new ArgumentException("必须配置认证域", nameof(Realm));
            }
            if (_relayAddress == null)
            {
                throw new ArgumentException("必须配置中继地址", nameof(RelayAddress));
            }
            if (_credentialTtlSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(CredentialTtlSeconds), "凭据有效期必须大于 0");
            }
            if (_nonceLifetimeSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(NonceLifetimeSeconds), "nonce 有效期必须大于 0");
            }
            if (_relayPortMin < 1 || _relayPortMax < _relayPortMin || _relayPortMax > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(RelayPortMin), "中继端口区间不合法");
            }
            if (_defaultLifetimeSeconds == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(DefaultLifetimeSeconds), "默认生命周期必须大于 0");
            }
            if (_maxLifetimeSeconds < _defaultLifetimeSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxLifetimeSeconds), "生命周期上限不能小于默认值");
            }
            if (_maxAllocations <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxAllocations), "分配总数上限必须大于 0");
            }
            if (_maxAllocationsPerUser <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxAllocationsPerUser), "单用户分配上限必须大于 0");
            }
            if (_connectionBindTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(ConnectionBindTimeout), "ConnectionBind 超时时间必须大于 0");
            }
            if (_relayConnectTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(RelayConnectTimeout), "中继连接超时时间必须大于 0");
            }
        }
    }
}