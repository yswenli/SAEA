using System;
using System.Threading;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 出站终态：完整帧字节加上背衬缓冲的归还有效载体。
    /// 所有权在 <see cref="IRawByteStream.SendAsync"/> 正常返回后交给传输层，链头不再释放 <see cref="Owner"/>。
    /// </summary>
    internal sealed class TransportFrame : IDisposable
    {
        private IDisposable _owner;
        private int _disposed;

        /// <summary>
        /// 以完整帧字节与其背衬缓冲的持有者创建出站终态。
        /// <paramref name="owner"/> 允许为 null（帧字节不来自池时没有可归还的缓冲）。
        /// </summary>
        internal TransportFrame(ReadOnlyMemory<byte> data, IDisposable owner)
        {
            Data = data;
            _owner = owner;
        }

        /// <summary>完整帧字节（含帧头）。</summary>
        public ReadOnlyMemory<byte> Data { get; }

        /// <summary>背衬缓冲的持有者，发送完成后由传输层释放；没有可归还缓冲时为 null。</summary>
        public IDisposable Owner
        {
            get { return _owner; }
        }

        /// <summary>幂等：只释放 <see cref="Owner"/>，重复调用不再重复释放。</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            var owner = Interlocked.Exchange(ref _owner, null);
            if (owner != null)
                owner.Dispose();
        }
    }
}