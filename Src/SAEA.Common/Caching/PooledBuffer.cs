/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Common.Caching
*类 名 称：PooledBuffer
*版本号： v26.4.23.1
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/4/18
*描述：
*=====================================================================
*修改时间：2026/4/18
*修 改 人： yswenli
*版本号： v26.4.23.1
*描    述：
*****************************************************************************/
using System;
using System.Buffers;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 池化缓冲区，支持自动归还
    /// </summary>
    public sealed class PooledBuffer : IDisposable
    {
        private readonly ArrayPool<byte> _pool;
        private bool _disposed;

        /// <summary>
        /// 缓冲区字节数组
        /// </summary>
        public byte[] Buffer { get; }

        /// <summary>
        /// 有效数据长度
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// 缓冲区容量
        /// </summary>
        public int Capacity => Buffer.Length;

        /// <summary>
        /// 缓冲区大小分级
        /// </summary>
        public BufferSizeTier Tier { get; }

        /// <summary>
        /// 创建池化缓冲区
        /// </summary>
        /// <param name="buffer">字节数组</param>
        /// <param name="length">有效数据长度</param>
        /// <param name="tier">大小分级</param>
        /// <param name="pool">所属池</param>
        public PooledBuffer(byte[] buffer, int length, BufferSizeTier tier, ArrayPool<byte> pool)
        {
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Length = length;
            Tier = tier;
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _disposed = false;
        }

        /// <summary>
        /// 获取有效数据的 Span
        /// </summary>
        public Span<byte> AsSpan() => Buffer.AsSpan(0, Length);

        /// <summary>
        /// 获取有效数据的 Memory
        /// </summary>
        public Memory<byte> AsMemory() => Buffer.AsMemory(0, Length);

        /// <summary>
        /// 归还缓冲区到池中
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _pool.Return(Buffer);
                _disposed = true;
            }
        }
    }
}
