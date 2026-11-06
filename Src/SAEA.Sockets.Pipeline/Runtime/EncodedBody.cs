using System;
using System.Threading;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 出站中间体：已编码的 body 与它的类型码。只在出站链内部传递，
    /// 由 <see cref="CodecEncoderHandler"/> 产出，交给 <see cref="FrameEncoderHandler"/> 补包头后即可回收。
    /// <para>
    /// 所有权约定：创建者持有 <see cref="Owner"/>，在下游返回（或抛异常）后立刻 <see cref="Dispose"/>；
    /// 下游只读取 <see cref="Body"/>，不得跨 <c>await</c> 保存，也不得代为释放。
    /// </para>
    /// </summary>
    internal sealed class EncodedBody : IDisposable
    {
        private IDisposable _owner;
        private int _disposed;

        /// <summary>
        /// 以类型码、body 切片与背衬缓冲的持有者创建出站中间体。
        /// </summary>
        internal EncodedBody(int typeCode, ReadOnlyMemory<byte> body, IDisposable owner)
        {
            TypeCode = typeCode;
            Body = body;
            _owner = owner;
        }

        /// <summary>线上 1 字节类型码，由信封的 <see cref="Envelope.Type"/> 拷入。</summary>
        public int TypeCode { get; }

        /// <summary>已编码的 body，不含帧头；只在持有者释放前有效。</summary>
        public ReadOnlyMemory<byte> Body { get; }

        /// <summary>body 背衬缓冲的持有者，由创建者在中间体释放时一并释放。</summary>
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