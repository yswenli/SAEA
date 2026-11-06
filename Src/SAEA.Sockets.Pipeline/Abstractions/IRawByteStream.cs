using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 与传输层解耦的字节流接入面。核心包不感知具体 socket 类型。
    /// </summary>
    public interface IRawByteStream
    {
        /// <summary>连接标识。</summary>
        string ChannelId { get; }

        /// <summary>
        /// 输入管道。配置接收泵后，该 reader 由泵独占读取；
        /// 同一连接不得再有第二个读者，否则会互相抢数据。
        /// </summary>
        PipeReader Input { get; }

        /// <summary>
        /// 把完整帧交给传输层。<paramref name="owner"/> 在底层发送真正完成或失败后
        /// 由传输层释放；本 Task 表示所有权已交接，不表示对端已收到。
        /// 连接已关闭时必须立即抛 <see cref="ChannelClosedException"/>，不得排队或静默成功。
        /// </summary>
        Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner);

        /// <summary>同步暂停读取，独立于传输层自身背压。</summary>
        void Pause();

        /// <summary>同步恢复读取。</summary>
        void Resume();

        /// <summary>关闭底层连接，必须幂等。</summary>
        Task CloseAsync(string reason);
    }
}