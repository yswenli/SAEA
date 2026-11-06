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
 *命名空间：SAEA.MVC
 *文件名： MVCWebSocketConnection
 *描述：同端口 WebSocket 连接
 *
 *****************************************************************************/
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Http.Base;
using SAEA.Http.Model;
using SAEA.Sockets.Base;
using SAEA.WebSocket.Model;
using SAEA.WebSocket.Type;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MVC
{
    /// <summary>
    /// 一条已升级的 WebSocket 连接：
    /// 负责把收到的原始字节解码、串行分发到 WSController，以及统一的关闭收尾。
    /// </summary>
    internal sealed class MVCWebSocketConnection : IWebSocketConnection
    {
        const int MaxQueuedFrames = 1024;

        internal const int MaxQueuedBytes = 4 * 1024 * 1024;

        readonly IWebSocketTransport _transport;

        readonly WSCoder _coder;

        readonly IHttpContext _context;

        readonly WSController _controller;

        readonly ConcurrentQueue<FrameItem> _queue = new ConcurrentQueue<FrameItem>();

        readonly SemaphoreSlim _signal = new SemaphoreSlim(0);

        int _queuedBytes;

        int _closed;

        int _state = (int)WebSocketState.Open;

        /// <summary>
        /// 上下文释放引用：泵与关闭收尾各持有一个，两者都结束后才 Dispose
        /// </summary>
        int _contextRefs = 2;

        /// <summary>
        /// 底层会话标识
        /// </summary>
        public string Id => _transport.SessionId;

        /// <summary>
        /// 当前状态
        /// </summary>
        public WebSocketState State => (WebSocketState)Volatile.Read(ref _state);

        /// <summary>
        /// 底层 socket 是否仍然存在（用于清理错过断开通知的残项）
        /// </summary>
        internal bool IsAlive => _transport.IsConnected;

        public MVCWebSocketConnection(IWebSocketTransport transport, WSCoder coder, IHttpContext context, WSController controller)
        {
            _transport = transport;
            _coder = coder;
            _context = context;
            _controller = controller;

            // 每条连接一个常驻串行泵，保证 MessageAsync 按到达顺序执行且不阻塞 saea 接收线程
            Task.Run((Func<Task>)PumpAsync);
        }

        #region 接收

        /// <summary>
        /// 接收原始字节（在 saea 接收线程调用），只做解码与入队，不执行业务
        /// </summary>
        internal void OnData(byte[] data)
        {
            if (data == null || data.Length == 0 || Volatile.Read(ref _closed) != 0) return;

            DecodedFrames frames = null;

            try
            {
                frames = _coder.Decode(new ReadOnlySequence<byte>(data));

                var span = frames.Frames;

                for (var i = 0; i < span.Length; i++)
                {
                    if (!(span[i] is WSProtocal protocal)) continue;

                    var opcode = protocal.Type;

                    switch ((WSProtocalType)opcode)
                    {
                        case WSProtocalType.Ping:
                            // 协议层自动回 Pong，不进入业务代码
                            TrySendFrame(WSProtocalType.Pong, protocal.Content.Span.ToArray());
                            break;

                        case WSProtocalType.Pong:
                            break;

                        case WSProtocalType.Close:
                            Enqueue(opcode, protocal.Content.Span.ToArray());
                            break;

                        case WSProtocalType.Text:
                        case WSProtocalType.Binary:
                            Enqueue(opcode, protocal.Content.Span.ToArray());
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Fail(ex, WSCloseReason.ProtocolError);
            }
            finally
            {
                // 帧不可跨批次持有：payload 已复制进队列，立即归还池化缓冲
                frames?.Dispose();
            }
        }

        void Enqueue(byte opcode, byte[] payload)
        {
            if (Volatile.Read(ref _closed) != 0) return;

            var size = payload?.Length ?? 0;

            var total = Interlocked.Add(ref _queuedBytes, size);

            if (_queue.Count >= MaxQueuedFrames || total > MaxQueuedBytes)
            {
                Interlocked.Add(ref _queuedBytes, -size);
                Fail(new InvalidOperationException("WebSocket 待处理消息积压超过上限，连接已关闭"), WSCloseReason.ServerShutdown);
                return;
            }

            _queue.Enqueue(new FrameItem(opcode, payload ?? Array.Empty<byte>()));

            try
            {
                _signal.Release();
            }
            catch (SemaphoreFullException)
            {
            }
        }

        async Task PumpAsync()
        {
            try
            {
                while (true)
                {
                    await _signal.WaitAsync().ConfigureAwait(false);

                    if (Volatile.Read(ref _closed) != 0) return;

                    if (!_queue.TryDequeue(out var item)) continue;

                    Interlocked.Add(ref _queuedBytes, -item.Payload.Length);

                    if (item.Opcode == (byte)WSProtocalType.Close)
                    {
                        await CloseCoreAsync(ParseCloseReason(item.Payload), sendClose: true).ConfigureAwait(false);
                        return;
                    }

                    try
                    {
                        SetCurrentContext();

                        var message = new WSMessage(
                            item.Opcode == (byte)WSProtocalType.Text ? WSMessageType.Text : WSMessageType.Binary,
                            item.Payload);

                        await _controller.MessageAsync(message).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Fail(ex, WSCloseReason.ServerShutdown);
                        return;
                    }
                    finally
                    {
                        ClearCurrentContext();
                    }
                }
            }
            finally
            {
                // 泵退出后再释放它持有的上下文引用：
                // CleanupAsync 可能在 saea 线程上结束，此时 MessageAsync 可能仍在执行
                ReleaseContext();
            }
        }

        #endregion

        #region 发送

        /// <summary>
        /// 发送文本消息
        /// </summary>
        public Task SendAsync(string text)
        {
            return SendFrameAsync(WSProtocalType.Text, Encoding.UTF8.GetBytes(text ?? string.Empty));
        }

        /// <summary>
        /// 发送二进制消息
        /// </summary>
        public Task SendAsync(byte[] data)
        {
            return SendFrameAsync(WSProtocalType.Binary, data ?? Array.Empty<byte>());
        }

        Task SendFrameAsync(WSProtocalType type, byte[] payload)
        {
            if (State != WebSocketState.Open) return Task.CompletedTask;

            try
            {
                // 服务端方向不掩码；底层发送自带写信号量，天然串行
                _transport.Send(BuildFrame(type, payload));
            }
            catch (Exception ex)
            {
                Fail(ex, WSCloseReason.ServerShutdown);
            }

            return Task.CompletedTask;
        }

        bool TrySendFrame(WSProtocalType type, byte[] payload)
        {
            try
            {
                _transport.Send(BuildFrame(type, payload));
                return true;
            }
            catch
            {
                return false;
            }
        }

        static byte[] BuildFrame(WSProtocalType type, byte[] payload)
        {
            using (var writer = new PooledBufferWriter(64))
            {
                var protocal = new WSProtocal(type, payload ?? Array.Empty<byte>());
                protocal.WriteTo(writer);
                return writer.WrittenSpan.ToArray();
            }
        }

        static byte[] BuildClosePayload(WSCloseReason reason)
        {
            var code = (int)reason;

            // RFC 6455：1006 不允许出现在 Close 帧里
            if (code == 1006) code = 1000;

            return new[] { (byte)(code >> 8), (byte)code };
        }

        static WSCloseReason ParseCloseReason(byte[] payload)
        {
            if (payload == null || payload.Length < 2) return WSCloseReason.Normal;

            var code = (payload[0] << 8) | payload[1];

            switch (code)
            {
                case 1000: return WSCloseReason.Normal;
                case 1001: return WSCloseReason.GoingAway;
                case 1002: return WSCloseReason.ProtocolError;
                case 1011: return WSCloseReason.ServerShutdown;
                default: return WSCloseReason.Normal;
            }
        }

        #endregion

        #region 关闭

        /// <summary>
        /// 服务端主动关闭：发 Close 帧后断开
        /// </summary>
        public Task CloseAsync(WSCloseReason reason = WSCloseReason.Normal)
        {
            return CloseCoreAsync(reason, sendClose: reason != WSCloseReason.Abnormal);
        }

        /// <summary>
        /// 立即中止，不发送 Close 帧
        /// </summary>
        public void Abort()
        {
            if (Interlocked.CompareExchange(ref _closed, 1, 0) != 0) return;

            Volatile.Write(ref _state, (int)WebSocketState.Closed);

            Task.Run(() => CleanupAsync(WSCloseReason.Abnormal, sendClose: false));
        }

        /// <summary>
        /// 底层连接断开（由 WebHost.OnDisconnected 触发）
        /// </summary>
        internal void HandleDisconnected()
        {
            if (Interlocked.CompareExchange(ref _closed, 1, 0) != 0) return;

            Volatile.Write(ref _state, (int)WebSocketState.Closed);

            Task.Run(() => CleanupAsync(WSCloseReason.Abnormal, sendClose: false));
        }

        internal void Fail(Exception exception, WSCloseReason reason)
        {
            if (Interlocked.CompareExchange(ref _closed, 1, 0) != 0) return;

            Volatile.Write(ref _state, (int)WebSocketState.Closing);

            Task.Run(() => FailAsync(exception, reason));
        }

        async Task FailAsync(Exception exception, WSCloseReason reason)
        {
            try
            {
                try
                {
                    SetCurrentContext();
                    await _controller.ErrorAsync(exception).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogHelper.Error("WebSocket ErrorAsync 回调异常", ex);
                }
                finally
                {
                    ClearCurrentContext();
                }

                await CleanupAsync(reason, sendClose: true).ConfigureAwait(false);
            }
            finally
            {
                Volatile.Write(ref _state, (int)WebSocketState.Closed);
            }
        }

        async Task CloseCoreAsync(WSCloseReason reason, bool sendClose)
        {
            if (Interlocked.CompareExchange(ref _closed, 1, 0) != 0) return;

            Volatile.Write(ref _state, (int)WebSocketState.Closing);

            try
            {
                await CleanupAsync(reason, sendClose).ConfigureAwait(false);
            }
            finally
            {
                Volatile.Write(ref _state, (int)WebSocketState.Closed);
            }
        }

        /// <summary>
        /// 幂等收尾：调用方已通过 _closed 抢占成功
        /// </summary>
        async Task CleanupAsync(WSCloseReason reason, bool sendClose)
        {
            try
            {
                if (sendClose && reason != WSCloseReason.Abnormal)
                {
                    TrySendFrame(WSProtocalType.Close, BuildClosePayload(reason));

                    // Close 帧只是交给底层异步发送：先等它写完，再半关闭发送方向。
                    // 直接 Close 会让内核 reset 掉还没被客户端读走的 Close 帧
                    _transport.WaitSendCompleted(1000);
                    _transport.ShutdownSend();
                }

                try
                {
                    _transport.SetNoTimeout(false);
                }
                catch
                {
                }

                WebSocketsFeature.Unregister(Id);

                try
                {
                    SetCurrentContext();
                    await _controller.CloseAsync(reason).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogHelper.Error("WebSocket CloseAsync 回调异常", ex);
                }
                finally
                {
                    ClearCurrentContext();
                }

                try
                {
                    _transport.Disconnect();
                }
                catch
                {
                }

                // 不在这里 Clear coder：saea 线程可能还在 Decode，Clear 会把其内部缓冲清空
                // 上下文也不在这里 Dispose，见 ReleaseContext

                DrainQueue();

                // 唤醒可能还在等待的泵，让它看到 _closed 后退出
                try
                {
                    _signal.Release();
                }
                catch (SemaphoreFullException)
                {
                }
            }
            finally
            {
                // 关闭回调结束后释放收尾持有的上下文引用；泵仍在执行时由泵释放最后一份
                ReleaseContext();
            }
        }

        /// <summary>
        /// 释放一份上下文引用，最后一份释放时才 Dispose
        /// </summary>
        void ReleaseContext()
        {
            if (Interlocked.Decrement(ref _contextRefs) != 0) return;

            try
            {
                _context.Dispose();
            }
            catch
            {
            }
        }

        void DrainQueue()
        {
            while (_queue.TryDequeue(out _))
            {
            }

            Interlocked.Exchange(ref _queuedBytes, 0);
        }

        #endregion

        #region 上下文

        void SetCurrentContext()
        {
            // WS 分发发生在 saea 线程或线程池任务上，必须恢复当前上下文
            CallContext<IHttpContext>.SetData("ContextBase.Current", _context);
        }

        static void ClearCurrentContext()
        {
            CallContext<IHttpContext>.SetData("ContextBase.Current", null);
        }

        #endregion

        readonly struct FrameItem
        {
            public readonly byte Opcode;

            public readonly byte[] Payload;

            public FrameItem(byte opcode, byte[] payload)
            {
                Opcode = opcode;
                Payload = payload;
            }
        }
    }
}