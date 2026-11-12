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
*命名空间：SAEA.P2P.Ice
*文件名： IceInboundResponder
*版本号： v26.11.5.1
*唯一标识：d5f56e8a-392f-43bf-816f-859dfa7a04e5
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 14:31:40
*描述：UDP 入向 ICE 检查应答器
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 14:31:40
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 UDP 入向 ICE 检查应答器
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;
using SAEA.P2P.Stun;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// UDP 入向检查应答器：对本端各传输上收到的 ICE Binding 请求校验 USERNAME 与 MESSAGE-INTEGRITY，
    /// 回带 XOR-MAPPED-ADDRESS 的成功应答，并把请求的来源、PRIORITY 与 USE-CANDIDATE 交给上层会话处理。
    /// 只做应答与上报，不建候选对、不提名、不发布选中对。
    /// </summary>
    public sealed class IceInboundResponder : IDisposable
    {
        /// <summary>
        /// 日志节点名
        /// </summary>
        private const string LogNode = "IceInboundResponder";

        /// <summary>
        /// 待订阅的传输集合，构造时固定
        /// </summary>
        private readonly List<IUdpTransport> _transports = new List<IUdpTransport>();

        /// <summary>
        /// 每个传输对应的订阅委托；退订时必须是同一实例
        /// </summary>
        private readonly Dictionary<IUdpTransport, Action<IPEndPoint, ReadOnlyMemory<byte>>> _subscriptions =
            new Dictionary<IUdpTransport, Action<IPEndPoint, ReadOnlyMemory<byte>>>();

        /// <summary>
        /// 短凭据 HMAC 密钥，构造时派生一次避免逐包重复计算
        /// </summary>
        private readonly byte[] _integrityKey;

        /// <summary>
        /// 本端 ufrag，用于校验 USERNAME 的后缀
        /// </summary>
        private readonly string _localUfrag;

        /// <summary>
        /// 是否已订阅
        /// </summary>
        private bool _started;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 已应答的检查数
        /// </summary>
        private int _handledRequestCount;

        /// <summary>
        /// 保存传输与本端短凭据，构造阶段不订阅事件
        /// </summary>
        /// <param name="transports">需要应答的传输集合</param>
        /// <param name="localUfrag">本端 ufrag</param>
        /// <param name="localPassword">本端短凭据口令</param>
        /// <exception cref="ArgumentNullException">transports 或 localPassword 为 null</exception>
        /// <exception cref="ArgumentException">localUfrag 为空、localPassword 为空字符串或 transports 中没有可用传输</exception>
        public IceInboundResponder(IEnumerable<IUdpTransport> transports, string localUfrag, string localPassword)
        {
            if (transports == null) throw new ArgumentNullException(nameof(transports));
            if (string.IsNullOrEmpty(localUfrag)) throw new ArgumentException("本地 ufrag 不能为空", nameof(localUfrag));
            if (localPassword == null) throw new ArgumentNullException(nameof(localPassword));
            if (localPassword.Length == 0) throw new ArgumentException("密码不能为空", nameof(localPassword));

            foreach (var transport in transports)
            {
                if (transport == null) continue;
                _transports.Add(transport);
            }
            if (_transports.Count == 0) throw new ArgumentException("至少需要一个本地传输", nameof(transports));

            _localUfrag = localUfrag;
            _integrityKey = StunIntegrity.CreateShortTermKey(localPassword);
        }

        /// <summary>
        /// 已应答的 Binding 请求数
        /// </summary>
        public int HandledRequestCount
        {
            get { return Volatile.Read(ref _handledRequestCount); }
        }

        /// <summary>
        /// 完整性校验通过的检查请求到达时触发；回调在接收线程同步执行，订阅者需自行保证线程安全
        /// </summary>
        public event Action<IUdpTransport, IPEndPoint, uint, bool> CheckRequestReceived;

        /// <summary>
        /// 订阅各传输的数据报事件，可重复调用
        /// </summary>
        /// <exception cref="ObjectDisposedException">已释放</exception>
        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (_started) return;
            _started = true;

            for (var i = 0; i < _transports.Count; i++)
            {
                var transport = _transports[i];
                if (transport == null) continue;

                // 应答必须从收到该请求的传输发出：中继通道与基线 socket 的源端口不同，回错传输对端会丢弃
                var target = transport;
                Action<IPEndPoint, ReadOnlyMemory<byte>> handler = (source, data) => OnDatagram(target, source, data);
                _subscriptions[target] = handler;
                target.DatagramReceived += handler;
            }
        }

        /// <summary>
        /// 退订全部传输事件并释放资源，可重复调用
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var subscription in _subscriptions)
            {
                try { subscription.Key.DatagramReceived -= subscription.Value; } catch { }
            }
            _subscriptions.Clear();
            _started = false;
        }

        /// <summary>
        /// 解析报文并回 Binding 成功响应；不能通过校验的报文直接丢弃
        /// </summary>
        /// <param name="transport">收到该报文的传输</param>
        /// <param name="source">报文的真实来源端点</param>
        /// <param name="data">原始报文，仅在回调期间有效</param>
        private void OnDatagram(IUdpTransport transport, IPEndPoint source, ReadOnlyMemory<byte> data)
        {
            var span = data.Span;

            // TURN ChannelData 与业务帧会复用同一 socket，先按 STUN 头部特征跳过非 STUN 报文
            if (span.Length < 20) return;
            if ((span[0] & 0xC0) != 0) return;

            StunMessage message;
            StunParseError error;
            if (!StunMessage.TryParse(span, out message, out error)) return;
            if (message == null) return;

            if (message.Method != StunMessageType.Binding || message.Class != StunClass.Request) return;

            StunAttribute usernameAttribute;
            if (!message.TryGet(StunAttributeType.Username, out usernameAttribute)) return;

            // USERNAME 形如 "远端ufrag:本端ufrag"，只认最后一段，远端 ufrag 本身允许含冒号
            var userName = Encoding.UTF8.GetString(usernameAttribute.Value.ToArray());
            var separator = userName.LastIndexOf(':');
            if (separator < 0 || separator == userName.Length - 1) return;
            var targetUfrag = userName.Substring(separator + 1);
            if (!string.Equals(targetUfrag, _localUfrag, StringComparison.Ordinal)) return;

            // 短凭据场景不回 401：凭据不符直接丢弃，避免把口令线索暴露给对端
            if (!StunIntegrity.TryVerify(message, _integrityKey)) return;

            // PRIORITY 缺失或长度不符时按 0 上报，订阅者拿不到对端优先级只能退化为最低优先级
            uint priority = 0;
            StunAttribute priorityAttribute;
            if (message.TryGet(StunAttributeType.Priority, out priorityAttribute))
            {
                StunAttributeCodec.TryGetUInt32(priorityAttribute, out priority);
            }

            bool useCandidate = message.TryGet(StunAttributeType.UseCandidate, out _);

            Interlocked.Increment(ref _handledRequestCount);
            RaiseCheckRequestReceived(transport, source, priority, useCandidate);

            var transactionId = message.TransactionId.ToArray();
            var response = new StunMessage(StunMessageType.Binding, StunClass.SuccessResponse, transactionId);
            response.SetAttributes(new[]
            {
                StunAddressCodec.EncodeAddress(StunAttributeType.XorMappedAddress, source, true, transactionId)
            });

            // 顺序固定：MESSAGE-INTEGRITY 覆盖到自身末尾，FINGERPRINT 必须在它之后且位于末位
            StunIntegrity.Append(response, _integrityKey);
            StunFingerprint.Append(response);

            // 发送失败只记日志，不能让回调抛异常打断接收循环
            var sendTask = transport.SendToAsync(source, new ReadOnlyMemory<byte>(response.ToArray()), CancellationToken.None);
            sendTask.ContinueWith(t =>
            {
                var ignored = t.Exception;
                P2PLogHelper.Debug(LogNode, "应答发送失败");
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }

        /// <summary>
        /// 触发请求到达事件；订阅者异常只记日志，不能让应答器停止处理后续请求
        /// </summary>
        /// <param name="transport">收到请求的传输</param>
        /// <param name="source">请求来源端点</param>
        /// <param name="priority">请求 PRIORITY 属性的值，缺失时为 0</param>
        /// <param name="useCandidate">请求是否携带 USE-CANDIDATE，即对端是否在提名该候选对</param>
        private void RaiseCheckRequestReceived(IUdpTransport transport, IPEndPoint source, uint priority, bool useCandidate)
        {
            var handler = CheckRequestReceived;
            if (handler == null) return;

            try
            {
                handler(transport, source, priority, useCandidate);
            }
            catch (Exception)
            {
                P2PLogHelper.Debug(LogNode, "检查请求订阅者处理失败");
            }
        }
    }
}
