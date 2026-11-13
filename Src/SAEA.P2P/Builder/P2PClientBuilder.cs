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
*修改时间：2026/11/13 12:10:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：改为聚合信令与 ICE 配置并直接构建客户端
*
*****************************************************************************/
using System;
using System.Net;
using SAEA.P2P.Common;
using SAEA.P2P.Core;
using SAEA.P2P.Ice;
using SAEA.P2P.Signaling;
using SAEA.P2P.Turn;

namespace SAEA.P2P.Builder
{
    /// <summary>
    /// P2P 客户端配置构建器，链式装配信令与 ICE/TURN 参数，BuildClient 直接产出客户端
    /// </summary>
    public class P2PClientBuilder
    {
        private SignalingOptions _signaling = new SignalingOptions();
        private IceSessionOptions _ice = new IceSessionOptions();

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
            _signaling.ServerAddress = address;
            _signaling.ServerPort = port;
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
            _signaling.ServerAddress = endpoint.Address.ToString();
            _signaling.ServerPort = endpoint.Port;
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
            _signaling.PeerId = nodeId;
            return this;
        }

        /// <summary>
        /// 设置节点鉴权密码，非空时注册阶段启用挑战应答认证
        /// </summary>
        /// <param name="password">鉴权密码，null 视为空字符串即不启用认证</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder SetNodeIdPassword(string password)
        {
            _signaling.SharedSecret = password ?? string.Empty;
            return this;
        }

        /// <summary>
        /// 设置连接信令服务器的超时时间
        /// </summary>
        /// <param name="timeoutMs">连接超时毫秒数，必须大于 0</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder SetTimeout(int timeoutMs)
        {
            if (timeoutMs <= 0)
            {
                throw new P2PException(ErrorCode.RegisterTimeout, "Timeout must be greater than 0");
            }
            _signaling.ConnectTimeoutMs = timeoutMs;
            return this;
        }

        /// <summary>
        /// 启用信令连接的 TLS，证书校验用的目标主机名在构建时补为服务器地址
        /// </summary>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder EnableTls()
        {
            _signaling.EnableTls = true;
            return this;
        }

        /// <summary>
        /// 整体替换信令配置，用于设置构建器未覆盖的高级选项
        /// </summary>
        /// <param name="signaling">信令配置，不能为 null</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder UseSignaling(SignalingOptions signaling)
        {
            if (signaling == null)
            {
                throw new ArgumentNullException(nameof(signaling));
            }
            _signaling = signaling;
            return this;
        }

        /// <summary>
        /// 整体替换 ICE 会话配置，用于调整收集预算与阶段预算
        /// </summary>
        /// <param name="ice">ICE 会话配置，不能为 null</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder UseIce(IceSessionOptions ice)
        {
            if (ice == null)
            {
                throw new ArgumentNullException(nameof(ice));
            }
            _ice = ice;
            return this;
        }

        /// <summary>
        /// 设置 TURN 中继配置，作为 ICE 选路的回退路径
        /// </summary>
        /// <param name="turn">TURN 客户端配置，不能为 null；不调用本方法时 TURN 阶段不可用</param>
        /// <returns>当前构建器，便于链式调用</returns>
        public P2PClientBuilder UseTurn(TurnClientOptions turn)
        {
            if (turn == null)
            {
                throw new ArgumentNullException(nameof(turn));
            }
            _ice.Turn = turn;
            return this;
        }

        /// <summary>
        /// 校验当前配置并构建客户端，校验失败抛异常
        /// </summary>
        /// <returns>已装配信令与 ICE 配置的客户端实例</returns>
        /// <exception cref="P2PException">节点标识为空、信令配置非法，或当前平台不支持 TCP 同时打开</exception>
        /// <exception cref="ArgumentException">ICE 会话配置非法</exception>
        public P2PClient BuildClient()
        {
            if (string.IsNullOrWhiteSpace(_signaling.PeerId))
            {
                throw new P2PException(ErrorCode.RegisterPeerIdMissing, "节点标识不能为空");
            }

            _signaling.Validate();

            if (_signaling.EnableTls && string.IsNullOrWhiteSpace(_signaling.TlsTargetHost))
            {
                _signaling.TlsTargetHost = _signaling.ServerAddress;
            }

            _ice.Validate();

            if (_ice.Ice != null && _ice.Ice.EnableTcpSimultaneousOpen && !TcpCandidateGatherer.IsSimultaneousOpenSupported)
            {
                throw new P2PException(ErrorCode.IceTcpSimultaneousOpenUnsupported, "当前平台不支持 TCP 同时打开（Windows 的 TCP 栈不支持），请关闭 EnableTcpSimultaneousOpen");
            }

            return new P2PClient(_signaling, _ice);
        }
    }
}