using System;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 第一层分帧：从连续缓冲中切出完整帧。实现不得产生协议元数据，
    /// 只按配置决定是否剥掉包头。
    /// </summary>
    public interface IFrameSplitter
    {
        /// <summary>
        /// 从当前连续缓冲里切出下一帧。返回 false 表示数据不足，需要继续读取，
        /// 此时 <paramref name="consumed"/> 与 <paramref name="frame"/> 的值无效。
        /// </summary>
        /// <param name="buffered">自上次消费位置起的未消费连续缓冲。</param>
        /// <param name="consumed">本次应从缓冲中消费掉的字节数（含被剥离的包头）。</param>
        /// <param name="frame">产出的帧切片，背衬内存由调用方持有，实现不得保存该切片。</param>
        bool TrySplit(ReadOnlyMemory<byte> buffered, out int consumed, out ReadOnlyMemory<byte> frame);

        /// <summary>
        /// 可接受的最大帧长。按帧体的口径解释；超限抛
        /// <see cref="FrameProtocolException"/>，用于防止内存被超大帧耗尽。
        /// </summary>
        int MaxFrameLength { get; }

        /// <summary>连接断开或握手失败时清空内部状态。</summary>
        void Reset();
    }
}