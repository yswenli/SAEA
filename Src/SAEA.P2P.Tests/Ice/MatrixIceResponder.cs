/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Tests.Ice
*文件名： MatrixIceResponder
*版本号： net10.0
*唯一标识：adaa09aa-c197-4942-a833-e9bb20d520e5
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 11:11:14
*描述：ICE 矩阵测试用的被动侧 STUN Binding 应答器
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 11:11:14
*修改人： yswenli
*版本号： net10.0
*描述：新增检查请求到达事件与订阅者失败计数，供矩阵测试按请求来源登记 peer-reflexive 候选
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Stun;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// 被动侧应答器：对收到的 ICE 连通性检查回最小 Binding 成功响应，并统计处理结果供测试断言。
    /// </summary>
    public sealed class MatrixIceResponder : IDisposable
    {
        /// <summary>
        /// 待订阅的传输集合，构造时固定
        /// </summary>
        private readonly List<IUdpTransport> _transports = new List<IUdpTransport>();

        /// <summary>
        /// 每个传输对应的订阅委托，退订时必须是同一实例
        /// </summary>
        private readonly Dictionary<IUdpTransport, Action<IPEndPoint, ReadOnlyMemory<byte>>> _subscriptions =
            new Dictionary<IUdpTransport, Action<IPEndPoint, ReadOnlyMemory<byte>>>();

        /// <summary>
        /// 短凭据 HMAC 密钥，构造时派生一次避免逐包重复计算
        /// </summary>
        private readonly byte[] _integrityKey;

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
        /// 完整性校验失败的报文数
        /// </summary>
        private int _integrityFailureCount;

        /// <summary>
        /// 响应发送失败次数
        /// </summary>
        private int _sendFailureCount;

        /// <summary>
        /// 带 USE-CANDIDATE 的检查数
        /// </summary>
        private int _useCandidateCount;

        /// <summary>
        /// 请求到达事件的订阅者失败次数
        /// </summary>
        private int _observerFailureCount;

        /// <summary>
        /// 保存传输与本地口令，构造阶段不订阅事件
        /// </summary>
        /// <param name="transports">需要应答的传输集合</param>
        /// <param name="localPassword">本地短凭据口令</param>
        /// <exception cref="ArgumentNullException">transports 或 localPassword 为 null</exception>
        /// <exception cref="ArgumentException">transports 为空集合或 localPassword 为空字符串</exception>
        public MatrixIceResponder(IEnumerable<IUdpTransport> transports, string localPassword)
        {
            if (transports == null) throw new ArgumentNullException(nameof(transports));
            if (localPassword == null) throw new ArgumentNullException(nameof(localPassword));
            if (localPassword.Length == 0) throw new ArgumentException(nameof(localPassword), nameof(localPassword));

            foreach (var transport in transports)
            {
                _transports.Add(transport);
            }
            if (_transports.Count == 0) throw new ArgumentException(nameof(transports), nameof(transports));

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
        /// MESSAGE-INTEGRITY 校验失败而被丢弃的报文数
        /// </summary>
        public int IntegrityFailureCount
        {
            get { return Volatile.Read(ref _integrityFailureCount); }
        }

        /// <summary>
        /// 响应发送失败次数
        /// </summary>
        public int SendFailureCount
        {
            get { return Volatile.Read(ref _sendFailureCount); }
        }

        /// <summary>
        /// 携带 USE-CANDIDATE 的检查数
        /// </summary>
        public int UseCandidateCount
        {
            get { return Volatile.Read(ref _useCandidateCount); }
        }

        /// <summary>
        /// 请求到达事件的订阅者抛出异常的次数；只计数不中断应答
        /// </summary>
        public int ObserverFailureCount
        {
            get { return Volatile.Read(ref _observerFailureCount); }
        }

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

                // 响应必须从收到该请求的传输发出：中继通道与基线 socket 的源端口不同，回错传输对端会丢弃
                var target = transport;
                Action<IPEndPoint, ReadOnlyMemory<byte>> handler = (source, data) => OnDatagram(target, source, data);
                _subscriptions[target] = handler;
                target.DatagramReceived += handler;
            }
        }

        /// <summary>
        /// 退订全部传输事件，可重复调用
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
        /// 解析报文并回最小 Binding 成功响应；非本端能处理的报文直接丢弃
        /// </summary>
        /// <param name="transport">收到该报文的传输</param>
        /// <param name="source">报文的真实来源端点</param>
        /// <param name="data">原始报文</param>
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

            // 短凭据场景不回 401：凭据不符直接丢弃，避免把口令线索暴露给对端
            if (!StunIntegrity.TryVerify(message, _integrityKey))
            {
                Interlocked.Increment(ref _integrityFailureCount);
                return;
            }

            if (message.TryGet(StunAttributeType.UseCandidate, out _))
            {
                Interlocked.Increment(ref _useCandidateCount);
            }

            // PRIORITY 缺失或长度不符时按 0 上报，订阅者拿不到对端优先级只能退化为最低优先级
            uint priority = 0;
            if (message.TryGet(StunAttributeType.Priority, out StunAttribute priorityAttribute))
            {
                StunAttributeCodec.TryGetUInt32(priorityAttribute, out priority);
            }
            OnCheckRequestReceived(transport, source, priority);

            var transactionId = message.TransactionId.ToArray();
            var response = new StunMessage(StunMessageType.Binding, StunClass.SuccessResponse, transactionId);
            response.SetAttributes(new[]
            {
                StunAddressCodec.EncodeAddress(StunAttributeType.XorMappedAddress, source, true, transactionId)
            });

            // 顺序固定：MESSAGE-INTEGRITY 覆盖到自身末尾，FINGERPRINT 必须在它之后且位于末位
            StunIntegrity.Append(response, _integrityKey);
            StunFingerprint.Append(response);

            Interlocked.Increment(ref _handledRequestCount);

            // 发送失败只计数，不能让回调抛异常打断接收循环
            var sendTask = SendResponseAsync(transport, source, response.ToArray());
            sendTask.ContinueWith(OnSendFaulted, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }

        /// <summary>
        /// 回发响应报文
        /// </summary>
        /// <param name="transport">收到请求的传输</param>
        /// <param name="destination">响应目标端点</param>
        /// <param name="payload">响应字节</param>
        /// <returns>发送完成的任务</returns>
        private async Task SendResponseAsync(IUdpTransport transport, IPEndPoint destination, byte[] payload)
        {
            await transport.SendToAsync(destination, new ReadOnlyMemory<byte>(payload), CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// 记录发送失败并观察异常，避免未观察异常在终结器线程上抛出
        /// </summary>
        /// <param name="task">发送任务</param>
        private void OnSendFaulted(Task task)
        {
            Interlocked.Increment(ref _sendFailureCount);
            var ignored = task.Exception;
        }

        /// <summary>
        /// 触发请求到达事件；订阅者异常只计数，不能让应答器停止处理后续请求
        /// </summary>
        /// <param name="transport">收到请求的传输</param>
        /// <param name="source">请求来源端点</param>
        /// <param name="priority">请求 PRIORITY 属性的值，缺失时为 0</param>
        private void OnCheckRequestReceived(IUdpTransport transport, IPEndPoint source, uint priority)
        {
            var handler = CheckRequestReceived;
            if (handler == null) return;

            try
            {
                handler(transport, source, priority);
            }
            catch (Exception)
            {
                // 观测者只做候选登记，失败只计数，不能让应答器停摆
                Interlocked.Increment(ref _observerFailureCount);
            }
        }

        /// <summary>
        /// 完整性校验通过的检查请求到达时触发；回调在接收线程同步执行，订阅者需自行保证线程安全
        /// </summary>
        public event Action<IUdpTransport, IPEndPoint, uint> CheckRequestReceived;
    }
}