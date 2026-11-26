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
 *命名空间：SAEA.Http.Base.Net
 *文件名： HttpConnectionSession
 *版本号： v26.11.5.1
 *唯一标识：5b0b9a2e-6f43-4b6d-8a8f-2f0f0f4b3c17
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/10 9:10:00
 *描述：单条 HTTP 连接的会话状态：通道、传输信息、协议屏障与在途发送计数
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/26 16:21:35
 *修改人： yswenli
 *版本号： v26.11.26.1
 *描述：新增接收 ReadOnlyMemory 的 WriteBytesAsync 与 WriteBytesSafeAsync，byte[] 重载委托给新重载
 *
 *****************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http.Base.Net
{
    /// <summary>
    /// 一条 HTTP 连接的会话状态，由管线激活时创建、失活时移除。
    /// <para>
    /// 连接级写一律经 <see cref="Pipeline"/> 完成：调用方只提交完整字节，编码、串行化与所有权移交都留在链上。
    /// 发送计数只服务于「关闭前等最后一次发送交接完成」，不参与协议判定。
    /// </para>
    /// </summary>
    internal sealed class HttpConnectionSession
    {
        /// <summary>通道属性键，供链上各处理器按通道取回会话。</summary>
        internal const string AttributeKey = "saea.http.session";

        private readonly object _writeGate = new object();

        private int _pendingWrites;

        private TaskCompletionSource<bool> _writeDrained;

        /// <summary>
        /// 创建连接会话。
        /// </summary>
        /// <param name="channel">连接通道视图</param>
        /// <param name="info">底层通道信息，可为 null</param>
        /// <param name="pipeline">本连接的处理器链</param>
        /// <param name="barrier">协议切换屏障，可为 null</param>
        /// <param name="upgradedSplitter">升级后生效的分帧器，可为 null</param>
        internal HttpConnectionSession(
            IChannel channel,
            ChannelInfo info,
            IChannelPipeline pipeline,
            SwitchableSplitter barrier,
            IFrameSplitter upgradedSplitter)
        {
            Channel = channel;
            Info = info;
            Pipeline = pipeline;
            Barrier = barrier;
            UpgradedSplitter = upgradedSplitter;
        }

        /// <summary>连接通道视图。</summary>
        internal IChannel Channel { get; private set; }

        /// <summary>底层通道信息，承载客户端 socket 与令牌；连接关闭后可能为 null。</summary>
        internal ChannelInfo Info { get; private set; }

        /// <summary>本连接的处理器链。</summary>
        internal IChannelPipeline Pipeline { get; private set; }

        /// <summary>协议切换屏障。</summary>
        internal SwitchableSplitter Barrier { get; private set; }

        /// <summary>升级后生效的分帧器。</summary>
        internal IFrameSplitter UpgradedSplitter { get; private set; }

        /// <summary>是否已完成协议升级。</summary>
        internal bool IsUpgraded { get; private set; }

        /// <summary>升级后的原始字节接收入口。</summary>
        internal Action<byte[]> RawSink { get; private set; }

        /// <summary>原始字节接收入口抛出的异常出口。</summary>
        internal Action<Exception> RawErrorSink { get; private set; }

        /// <summary>按通道取回当前连接会话；未登记返回 null。</summary>
        /// <param name="channel">连接通道视图</param>
        /// <returns>连接会话；未登记时为 null</returns>
        internal static HttpConnectionSession TryGet(IChannel channel)
        {
            if (channel == null)
                return null;

            return channel.GetAttribute(AttributeKey) as HttpConnectionSession;
        }

        /// <summary>
        /// 完成协议升级：先登记原始字节入口，再切换分帧器。
        /// 先登记是必须的：切换会立即唤醒接收泵，晚登记可能丢掉紧随 101 之后的帧。
        /// </summary>
        /// <param name="onData">升级后的原始字节入口</param>
        /// <param name="onError">原始字节入口抛出的异常出口</param>
        /// <exception cref="InvalidOperationException">缺少屏障或升级分帧器，或当前没有等待切换的帧</exception>
        internal void Upgrade(Action<byte[]> onData, Action<Exception> onError)
        {
            if (IsUpgraded)
                throw new InvalidOperationException("当前连接已完成 WebSocket 升级");

            var barrier = Barrier;
            var upgraded = UpgradedSplitter;
            if (barrier == null || upgraded == null)
                throw new NotSupportedException("当前连接缺少协议切换屏障，无法升级为 WebSocket");

            var previousSink = RawSink;
            var previousError = RawErrorSink;

            RawSink = onData;
            RawErrorSink = onError;

            try
            {
                barrier.SwitchTo(upgraded);
            }
            catch
            {
                // 切换失败时连接仍是 HTTP，必须把入口还原，避免后续帧被误当原始字节。
                RawSink = previousSink;
                RawErrorSink = previousError;
                throw;
            }

            IsUpgraded = true;
        }

        /// <summary>
        /// 阻塞写出一段完整字节；写失败只记录，不向业务抛出。
        /// </summary>
        /// <param name="data">待写出的字节</param>
        internal void WriteBytes(byte[] data)
        {
            WriteBytesSafeAsync(data).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步写出一段完整字节；写失败只记录，不向业务抛出。
        /// </summary>
        /// <param name="data">待写出的字节</param>
        /// <returns>写出完成后的任务</returns>
        internal Task WriteBytesSafeAsync(byte[] data)
        {
            if (data == null || data.Length == 0)
                return Task.CompletedTask;

            return WriteBytesSafeAsync((ReadOnlyMemory<byte>)data);
        }

        /// <summary>
        /// 异步写出一段完整字节（可以是池化缓冲中的一段）；写失败只记录，不向业务抛出。
        /// </summary>
        /// <param name="data">待写出的字节</param>
        /// <returns>写出完成后的任务</returns>
        internal async Task WriteBytesSafeAsync(ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0)
                return;

            try
            {
                await WriteBytesAsync(data).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHelper.Debug("HTTP 响应写出失败:" + (Channel == null ? string.Empty : Channel.Id) + "," + ex.Message);
            }
        }

        /// <summary>
        /// 阻塞写出一段完整字节，随后请求关闭连接。
        /// 关闭不等待：在入站回调内等待收口会与自身在途消息互等。
        /// </summary>
        /// <param name="data">待写出的字节，可为 null</param>
        internal void WriteBytesAndClose(byte[] data)
        {
            WriteBytesAndCloseAsync(data).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步写出一段完整字节，等写真正完成后才请求关闭连接。
        /// 写失败只记录，仍然关闭连接，避免半开连接悬挂。
        /// </summary>
        /// <param name="data">待写出的字节，可为 null</param>
        /// <returns>写完成并已提交关闭后的任务</returns>
        internal async Task WriteBytesAndCloseAsync(byte[] data)
        {
            await WriteBytesSafeAsync(data).ConfigureAwait(false);

            Close();
        }

        /// <summary>请求关闭连接，重复调用无副作用。</summary>
        internal void Close()
        {
            var channel = Channel;
            if (channel == null)
                return;

            _ = CloseCoreAsync(channel);
        }

        /// <summary>
        /// 异步发送原始字节（不阻塞调用方），并计入在途发送数。
        /// 发送失败只记录；调用方通过 <see cref="WaitSendCompleted"/> 等待交接完成。
        /// </summary>
        /// <param name="data">待发送字节</param>
        internal void SendRaw(byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            _ = SendRawCoreAsync(data);
        }

        /// <summary>
        /// 等待在途发送全部完成。
        /// </summary>
        /// <param name="timeoutMs">最长等待毫秒数，负数按 0 处理</param>
        /// <returns>在途发送已清零返回 true，超时返回 false</returns>
        internal bool WaitSendCompleted(int timeoutMs)
        {
            if (timeoutMs < 0)
                timeoutMs = 0;

            Task pending;
            lock (_writeGate)
            {
                if (_pendingWrites == 0)
                    return true;

                if (_writeDrained == null)
                    _writeDrained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                pending = _writeDrained.Task;
            }

            return pending.Wait(timeoutMs);
        }

        /// <summary>
        /// 异步写出一段完整字节；没有管线时抛出，调用方决定是否吞掉。
        /// </summary>
        /// <param name="data">待写出的字节</param>
        /// <returns>写出完成后的任务</returns>
        internal Task WriteBytesAsync(byte[] data)
        {
            return WriteBytesAsync((ReadOnlyMemory<byte>)data);
        }

        /// <summary>
        /// 异步写出一段完整字节（可以是池化缓冲中的一段）；没有管线时抛出，调用方决定是否吞掉。
        /// </summary>
        /// <param name="data">待写出的字节</param>
        /// <returns>写出完成后的任务</returns>
        internal async Task WriteBytesAsync(ReadOnlyMemory<byte> data)
        {
            var pipeline = Pipeline;
            if (pipeline == null)
                throw new ChannelClosedException("连接尚未完成管线装配，不能发送。");

            await pipeline.WriteAsync(new RawFrame(data, null)).ConfigureAwait(false);
        }

        /// <summary>
        /// 异步写出一段完整帧，背衬缓冲的所有权随帧移交；写失败只记录，未移交成功的缓冲当场归还。
        /// </summary>
        /// <param name="data">待写出的完整帧</param>
        /// <param name="owner">背衬缓冲的归还句柄</param>
        /// <returns>写出完成后的任务</returns>
        internal async Task WriteFrameSafeAsync(ReadOnlyMemory<byte> data, IDisposable owner)
        {
            if (data.Length == 0)
            {
                owner.Dispose();
                return;
            }

            try
            {
                await WriteFrameAsync(data, owner).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHelper.Debug("HTTP 响应写出失败:" + (Channel == null ? string.Empty : Channel.Id) + "," + ex.Message);
            }
        }

        /// <summary>
        /// 异步写出一段完整帧，等写真正完成后才请求关闭连接。
        /// </summary>
        /// <param name="data">待写出的完整帧</param>
        /// <param name="owner">背衬缓冲的归还句柄</param>
        /// <returns>写完成并已提交关闭后的任务</returns>
        internal async Task WriteFrameAndCloseAsync(ReadOnlyMemory<byte> data, IDisposable owner)
        {
            await WriteFrameSafeAsync(data, owner).ConfigureAwait(false);

            Close();
        }

        private async Task WriteFrameAsync(ReadOnlyMemory<byte> data, IDisposable owner)
        {
            var pipeline = Pipeline;
            if (pipeline == null)
            {
                // 没有管线就不可能完成移交，缓冲先归还再抛出，避免池化租约泄漏
                owner.Dispose();
                throw new ChannelClosedException("连接尚未完成管线装配，不能发送。");
            }

            var frame = new RawFrame(data, owner);

            try
            {
                await pipeline.WriteAsync(frame).ConfigureAwait(false);
            }
            catch
            {
                // 链上拒绝时不会替调用方释放，这里补一次；已移交时 Dispose 是空操作
                frame.Dispose();
                throw;
            }
        }

        private async Task SendRawCoreAsync(byte[] data)
        {
            lock (_writeGate)
            {
                _pendingWrites++;
            }

            try
            {
                await WriteBytesAsync(data).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHelper.Debug("WebSocket 发送失败:" + (Channel == null ? string.Empty : Channel.Id) + "," + ex.Message);
            }
            finally
            {
                TaskCompletionSource<bool> signal = null;

                lock (_writeGate)
                {
                    _pendingWrites--;

                    if (_pendingWrites <= 0)
                    {
                        _pendingWrites = 0;
                        signal = _writeDrained;
                        _writeDrained = null;
                    }
                }

                if (signal != null)
                    signal.TrySetResult(true);
            }
        }

        private static async Task CloseCoreAsync(IChannel channel)
        {
            try
            {
                await channel.CloseAsync("http-disconnect").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHelper.Debug("HTTP 连接关闭失败:" + channel.Id + "," + ex.Message);
            }
        }
    }
}