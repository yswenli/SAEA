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
*命名空间：SAEA.P2P.Stun
*文件名： NatBehaviorProbe
*版本号： v26.11.5.1
*唯一标识：66deb54f-5f03-4ea3-8ab2-1f2efb2a943e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 21:00:00
*描述：按 RFC 5780 探测 NAT 映射与过滤行为（只读）
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 21:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 NAT 行为探测
*
*****************************************************************************/
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Stun
{
    /// <summary>
    /// NAT 行为探测器：在一个已绑定的 UDP 传输上依次发出 RFC 5780 的映射与过滤探测。
    /// 探测结果只用于诊断展示，探测失败不影响连接流程；本类不拥有传输，释放时不关闭传输。
    /// </summary>
    public sealed class NatBehaviorProbe : IDisposable
    {
        /// <summary>
        /// CHANGE-REQUEST 同时换地址与换端口
        /// </summary>
        private const uint ChangeAddressAndPort = 0x06;

        /// <summary>
        /// CHANGE-REQUEST 只换端口
        /// </summary>
        private const uint ChangePortOnly = 0x02;

        /// <summary>
        /// 服务端不支持换地址换端口时返回的错误码
        /// </summary>
        private const int UnknownAttributeErrorCode = 420;

        /// <summary>
        /// 内部复用的 STUN 客户端
        /// </summary>
        private readonly StunClient _client;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 创建探测器
        /// </summary>
        /// <param name="transport">已绑定的 UDP 传输；本类只订阅其接收事件，不负责释放</param>
        /// <param name="clock">计时时钟；为 null 时使用系统时钟</param>
        /// <param name="software">SOFTWARE 属性值；为 null 时不携带</param>
        public NatBehaviorProbe(IUdpTransport transport, IRetryClock clock = null, string software = null)
        {
            if (transport == null)
            {
                throw new ArgumentNullException(nameof(transport));
            }

            _client = new StunClient(transport, clock, software);
        }

        /// <summary>
        /// 初始重传间隔，单位毫秒；用例可调小以缩短超时等待，默认 500ms
        /// </summary>
        public int RtoMilliseconds
        {
            get { return _client.RtoMilliseconds; }
            set { _client.RtoMilliseconds = value; }
        }

        /// <summary>
        /// 对指定 STUN 服务端执行映射与过滤探测；任何一步失败都返回 <see cref="NatBehaviorKind.Unknown"/>，不抛异常
        /// </summary>
        /// <param name="primaryServer">主 STUN 服务端端点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>探测结果</returns>
        /// <exception cref="ArgumentNullException">primaryServer 为 null</exception>
        /// <exception cref="ObjectDisposedException">已释放</exception>
        public async Task<NatBehaviorInfo> ProbeAsync(IPEndPoint primaryServer, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (primaryServer == null)
            {
                throw new ArgumentNullException(nameof(primaryServer));
            }

            ThrowIfDisposed();

            var baseline = await _client.BindingAsync(primaryServer, cancellationToken).ConfigureAwait(false);
            if (!baseline.Succeeded)
            {
                P2PLogHelper.Debug("NatBehaviorProbe", "基线探测失败：" + baseline.Failure);
                return NatBehaviorInfo.Create(null, primaryServer, null, NatBehaviorKind.Unknown, NatBehaviorKind.Unknown);
            }

            var alternate = baseline.OtherAddress;
            if (alternate == null || alternate.Port == 0)
            {
                // 单地址服务端不会给出 OTHER-ADDRESS，换地址与换端口都无从探测
                return NatBehaviorInfo.Create(baseline.MappedAddress, primaryServer, null, NatBehaviorKind.Unknown, NatBehaviorKind.Unknown);
            }

            var mapping = await ProbeMappingAsync(primaryServer, alternate, baseline.MappedAddress, cancellationToken).ConfigureAwait(false);
            var filtering = await ProbeFilteringAsync(primaryServer, cancellationToken).ConfigureAwait(false);

            return NatBehaviorInfo.Create(baseline.MappedAddress, primaryServer, alternate, mapping, filtering);
        }

        /// <summary>
        /// 释放内部客户端；不释放调用方的传输
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _client.Dispose();
        }

        /// <summary>
        /// 映射行为：比较「主地址主端口」「备用地址主端口」「备用地址备用端口」三次应答的映射地址
        /// </summary>
        /// <param name="primaryServer">主服务端端点</param>
        /// <param name="alternate">服务端给出的备用端点</param>
        /// <param name="baselineMapped">基线映射地址</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>映射行为</returns>
        private async Task<NatBehaviorKind> ProbeMappingAsync(IPEndPoint primaryServer, IPEndPoint alternate, IPEndPoint baselineMapped, CancellationToken cancellationToken)
        {
            var second = await _client.BindingAsync(new IPEndPoint(alternate.Address, primaryServer.Port), cancellationToken).ConfigureAwait(false);
            if (!second.Succeeded)
            {
                P2PLogHelper.Debug("NatBehaviorProbe", "映射探测第二项失败：" + second.Failure);
                return NatBehaviorKind.Unknown;
            }

            var third = await _client.BindingAsync(alternate, cancellationToken).ConfigureAwait(false);
            if (!third.Succeeded)
            {
                P2PLogHelper.Debug("NatBehaviorProbe", "映射探测第三项失败：" + third.Failure);
                return NatBehaviorKind.Unknown;
            }

            if (SameEndPoint(baselineMapped, second.MappedAddress))
            {
                return NatBehaviorKind.EndpointIndependent;
            }

            // 只有换端口后映射才变化，说明映射随目标地址相关
            if (SameEndPoint(second.MappedAddress, third.MappedAddress))
            {
                return NatBehaviorKind.AddressDependent;
            }

            return NatBehaviorKind.AddressAndPortDependent;
        }

        /// <summary>
        /// 过滤行为：先请求同时换地址与换端口，再请求只换端口
        /// </summary>
        /// <param name="primaryServer">主服务端端点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>过滤行为</returns>
        private async Task<NatBehaviorKind> ProbeFilteringAsync(IPEndPoint primaryServer, CancellationToken cancellationToken)
        {
            var changeAll = await SendChangeRequestAsync(primaryServer, ChangeAddressAndPort, cancellationToken).ConfigureAwait(false);
            if (changeAll.Succeeded)
            {
                return NatBehaviorKind.EndpointIndependent;
            }

            // 服务端明确不支持换地址时不能把失败归因于 NAT 过滤
            if (changeAll.ErrorCode == UnknownAttributeErrorCode)
            {
                return NatBehaviorKind.Unknown;
            }

            if (changeAll.Failure != StunBindingFailure.Timeout || cancellationToken.IsCancellationRequested)
            {
                return NatBehaviorKind.Unknown;
            }

            var changePort = await SendChangeRequestAsync(primaryServer, ChangePortOnly, cancellationToken).ConfigureAwait(false);
            if (changePort.Succeeded)
            {
                return NatBehaviorKind.AddressDependent;
            }

            if (changePort.Failure != StunBindingFailure.Timeout)
            {
                return NatBehaviorKind.Unknown;
            }

            return NatBehaviorKind.AddressAndPortDependent;
        }

        /// <summary>
        /// 携带 CHANGE-REQUEST 发出一次 Binding 请求
        /// </summary>
        /// <param name="server">服务端端点</param>
        /// <param name="flags">CHANGE-REQUEST 标志位</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>Binding 结果</returns>
        private Task<StunBindingResult> SendChangeRequestAsync(IPEndPoint server, uint flags, CancellationToken cancellationToken)
        {
            var attributes = new[] { StunAttributeCodec.CreateUInt32(StunAttributeType.ChangeRequest, flags) };
            return _client.BindingAsync(server, null, attributes, cancellationToken);
        }

        /// <summary>
        /// 判断两个端点是否完全一致；任一为 null 时视为不一致
        /// </summary>
        /// <param name="left">左端点</param>
        /// <param name="right">右端点</param>
        /// <returns>地址与端口都相同返回 true</returns>
        private static bool SameEndPoint(IPEndPoint left, IPEndPoint right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return left.Port == right.Port && left.Address.Equals(right.Address);
        }

        /// <summary>
        /// 已释放时拒绝新的探测
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}