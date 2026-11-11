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
*命名空间：SAEA.P2P.Transport
*文件名： UdpDataChannel
*版本号： v26.11.5.1
*唯一标识：ef1455e6-8f5e-4506-a556-e50144e1a182
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 19:00:00
*描述：UDP 直连数据面通道，收发业务帧并统计丢弃
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 19:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 UDP 直连数据面通道
*
*****************************************************************************/
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SAEA.P2P.Common;

namespace SAEA.P2P.Transport
{
    /// <summary>
    /// UDP 直连数据面通道：一个数据报一帧，收侧按首字节剥离 0xC0 前缀，发侧补前缀后交给传输层。
    /// 来源端点不做过滤，来源校验由 ICE 选中 pair 之后的上层负责。
    /// </summary>
    public sealed class UdpDataChannel : IDisposable
    {
        private readonly IUdpTransport _transport;

        private readonly IPEndPoint _remoteEndPoint;

        private readonly Action<IPEndPoint, ReadOnlyMemory<byte>> _datagramHandler;

        private readonly object _syncRoot = new object();

        private bool _started;

        private bool _disposed;

        private long _receivedFrameCount;

        private long _droppedDatagramCount;

        /// <summary>
        /// 收到一条完整业务帧时触发，参数为已复制的载荷，不含 0xC0 前缀；回调返回后仍可用
        /// </summary>
        public event Action<ReadOnlyMemory<byte>> FrameReceived;

        /// <summary>
        /// 已投递的业务帧数量
        /// </summary>
        public long ReceivedFrameCount
        {
            get { return Interlocked.Read(ref _receivedFrameCount); }
        }

        /// <summary>
        /// 已丢弃的数据报数量，STUN 报文与未知首字节都计入
        /// </summary>
        public long DroppedDatagramCount
        {
            get { return Interlocked.Read(ref _droppedDatagramCount); }
        }

        /// <summary>
        /// 在指定传输上创建到固定远端端点的数据通道
        /// </summary>
        /// <param name="transport">底层 UDP 传输</param>
        /// <param name="remoteEndPoint">对端端点</param>
        /// <exception cref="ArgumentNullException">transport 或 remoteEndPoint 为 null 时抛出</exception>
        public UdpDataChannel(IUdpTransport transport, IPEndPoint remoteEndPoint)
        {
            if (transport == null)
            {
                throw new ArgumentNullException(nameof(transport));
            }

            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            _transport = transport;
            _remoteEndPoint = remoteEndPoint;
            _datagramHandler = OnDatagramReceived;
        }

        /// <summary>
        /// 订阅传输层的数据报事件，幂等；重复调用不会重复订阅
        /// </summary>
        /// <exception cref="ObjectDisposedException">通道已释放时抛出</exception>
        public void Start()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(UdpDataChannel));
                }

                if (_started)
                {
                    return;
                }

                _transport.DatagramReceived += _datagramHandler;
                _started = true;
            }
        }

        /// <summary>
        /// 退订传输层的数据报事件，幂等
        /// </summary>
        public void Stop()
        {
            lock (_syncRoot)
            {
                if (!_started)
                {
                    return;
                }

                _transport.DatagramReceived -= _datagramHandler;
                _started = false;
            }
        }

        /// <summary>
        /// 把业务载荷封装成帧并交给传输层发送。
        /// 同步指本方法返回后调用方 buffer 即可释放，不是指等网络写完：发送任务不阻塞等待，失败只记调试日志。
        /// </summary>
        /// <param name="payload">业务载荷，长度不得超过 <see cref="DataPlaneCodec.MaxPayloadLength"/></param>
        /// <exception cref="P2PException">载荷超过单包上限时抛出，错误码为 PayloadTooLarge</exception>
        /// <exception cref="ObjectDisposedException">通道已释放时抛出</exception>
        public void Send(ReadOnlySpan<byte> payload)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UdpDataChannel));
            }

            var frame = DataPlaneCodec.Wrap(payload);
            var sendTask = _transport.SendToAsync(_remoteEndPoint, frame, CancellationToken.None);
            sendTask.ContinueWith(
                task => P2PLogHelper.Debug("UdpDataChannel", "发送业务帧失败：" + task.Exception.GetBaseException().Message),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        /// <summary>
        /// 释放通道并退订事件，幂等
        /// </summary>
        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            Stop();
        }

        /// <summary>
        /// 处理一个数据报：业务帧复制载荷后投递，其余只计数
        /// </summary>
        /// <param name="from">来源端点，本通道不据此过滤</param>
        /// <param name="datagram">原始数据报</param>
        private void OnDatagramReceived(IPEndPoint from, ReadOnlyMemory<byte> datagram)
        {
            ReadOnlySpan<byte> payload;
            if (DataPlaneCodec.Classify(datagram.Span) == DataPlaneKind.Data
                && DataPlaneCodec.TryUnwrap(datagram.Span, out payload))
            {
                var copy = payload.ToArray();
                Interlocked.Increment(ref _receivedFrameCount);
                var handler = FrameReceived;
                if (handler != null)
                {
                    handler(new ReadOnlyMemory<byte>(copy));
                }
                return;
            }

            Interlocked.Increment(ref _droppedDatagramCount);
        }
    }
}