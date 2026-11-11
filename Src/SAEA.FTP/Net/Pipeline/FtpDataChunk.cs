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
*命名空间：SAEA.FTP
*文件名： FtpDataChunk
*版本号： v26.11.5.1
*唯一标识：9ab2e49a-d226-49f9-8b3b-9c5b885e3d81
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 16:37:14
*描述：FTP 数据通道的数据块，自带池化缓冲所有权
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 16:37:14
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增数据通道的数据块类型
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Threading;
using SAEA.Common.Caching;

namespace SAEA.FTP
{
    /// <summary>
    /// FTP 数据通道的数据块：一段独立复制出来的字节加上池化缓冲的所有权。
    /// <para>
    /// 所有权约定：<see cref="FtpDataChunk"/> 自身就是缓冲的持有者。入站方向由适配器队列持有，
    /// 消费者 <see cref="ChannelStreamAdapter.Read"/> 取完后由适配器释放；出站方向随
    /// <see cref="SAEA.Sockets.Pipeline.RawFrame"/> 的 owner 移交给传输层，由传输层在发送完成或失败后释放。
    /// 任何一方在移交前失败都必须自己 <see cref="Dispose"/>。
    /// </para>
    /// <para>
    /// <see cref="Memory"/> 指向池化数组，<b>只在未释放期间有效</b>；<see cref="Dispose"/> 之后不得再读。
    /// 释放幂等，只归还一次。
    /// </para>
    /// </summary>
    public sealed class FtpDataChunk : IDisposable
    {
        private readonly PooledBufferWriter _writer;

        private int _disposed;

        /// <summary>
        /// 内部构造：空块不持有池化缓冲，避免为零长度数据占用一整块池内存。
        /// </summary>
        /// <param name="writer">背衬缓冲的池化写入器；空块为 null</param>
        /// <param name="memory">对外暴露的字节视图</param>
        /// <param name="length">有效字节数</param>
        private FtpDataChunk(PooledBufferWriter writer, ReadOnlyMemory<byte> memory, int length)
        {
            _writer = writer;
            Memory = memory;
            Length = length;
        }

        /// <summary>本块持有的字节视图；只在未释放期间有效。</summary>
        public ReadOnlyMemory<byte> Memory { get; }

        /// <summary>本块的字节数。</summary>
        public int Length { get; }

        /// <summary>池化缓冲是否已归还；重复释放后仍为 true。</summary>
        public bool IsDisposed
        {
            get { return Volatile.Read(ref _disposed) != 0; }
        }

        /// <summary>
        /// 把一段字节复制成数据块，用于出站写出。调用返回后源字节再被修改不影响本块。
        /// </summary>
        /// <param name="data">待复制的字节</param>
        /// <returns>持有独立池化副本的数据块</returns>
        public static FtpDataChunk CopyFrom(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
                return new FtpDataChunk(null, ReadOnlyMemory<byte>.Empty, 0);

            var writer = new PooledBufferWriter(data.Length);
            try
            {
                var span = writer.GetSpan(data.Length);
                data.CopyTo(span);
                writer.Advance(data.Length);
                return new FtpDataChunk(writer, writer.WrittenMemory, data.Length);
            }
            catch
            {
                // 构造未完成，所有权还没交出去，必须在这里归还。
                writer.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 把整个数组复制成数据块。
        /// </summary>
        /// <param name="data">待复制的字节数组，为 null 抛 <see cref="ArgumentNullException"/></param>
        /// <returns>持有独立池化副本的数据块</returns>
        public static FtpDataChunk CopyFrom(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return CopyFrom(new ReadOnlySpan<byte>(data));
        }

        /// <summary>
        /// 把数组的一段复制成数据块。
        /// </summary>
        /// <param name="data">源数组，为 null 抛 <see cref="ArgumentNullException"/></param>
        /// <param name="offset">起始下标，为负或超出数组长度抛 <see cref="ArgumentOutOfRangeException"/></param>
        /// <param name="count">字节数，为负或与 <paramref name="offset"/> 之和超出数组长度抛 <see cref="ArgumentOutOfRangeException"/></param>
        /// <returns>持有独立池化副本的数据块</returns>
        public static FtpDataChunk CopyFrom(byte[] data, int offset, int count)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (offset < 0 || offset > data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "offset 超出源数组范围。");
            if (count < 0 || data.Length - offset < count)
                throw new ArgumentOutOfRangeException(nameof(count), count, "count 超出源数组剩余长度。");

            return CopyFrom(new ReadOnlySpan<byte>(data, offset, count));
        }

        /// <summary>
        /// 把分帧器切出的一段连续字节复制成数据块，用于入站。
        /// 入参是链持有的池化内存，只能在当前回调内使用，所以必须整段深拷贝后再交给消费者。
        /// </summary>
        /// <param name="sequence">待复制的字节序列</param>
        /// <returns>持有独立池化副本的数据块</returns>
        public static FtpDataChunk CopyFrom(ReadOnlySequence<byte> sequence)
        {
            if (sequence.Length == 0)
                return new FtpDataChunk(null, ReadOnlyMemory<byte>.Empty, 0);
            if (sequence.Length > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(sequence), sequence.Length, "数据块长度超过 int 上限。");

            var writer = new PooledBufferWriter((int)sequence.Length);
            try
            {
                var written = 0;
                foreach (var segment in sequence)
                {
                    if (segment.Length == 0) continue;

                    var span = writer.GetSpan(segment.Length);
                    segment.Span.CopyTo(span);
                    writer.Advance(segment.Length);
                    written += segment.Length;
                }

                return new FtpDataChunk(writer, writer.WrittenMemory, written);
            }
            catch
            {
                // 构造未完成，所有权还没交出去，必须在这里归还。
                writer.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 归还池化缓冲；幂等，重复调用不会重复归还。
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            var writer = _writer;
            if (writer != null)
                writer.Dispose();
        }
    }
}