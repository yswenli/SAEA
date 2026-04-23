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
*命名空间：SAEA.Common.Caching
*文件名： MemoryPoolManager
*版本号： v26.4.23.1
*唯一标识：3fd211c8-a1a6-4a65-869c-ced9748f270d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/18 22:52:07
*描述：MemoryPoolManager管理类
*
*=====================================================================
*修改标记
*修改时间：2026/04/18 22:52:07
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MemoryPoolManager管理类
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Threading;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 内存池管理器，提供分层级内存池管理功能
    /// <para>支持小型（&lt;4KB）、中型（4KB-64KB）、大型（&gt;64KB）三种级别的缓冲区管理</para>
    /// </summary>
    public static class MemoryPoolManager
    {
        #region Constants

        /// <summary>
        /// 小型缓冲区阈值（4KB）
        /// <para>小于此值的缓冲区归为小型池</para>
        /// </summary>
        public const int SmallThreshold = 4 * 1024;

        /// <summary>
        /// 中型缓冲区阈值（64KB）
        /// <para>大于等于 SmallThreshold 且小于此值的缓冲区归为中型池</para>
        /// </summary>
        public const int MediumThreshold = 64 * 1024;

        /// <summary>
        /// 大型缓冲区阈值（1MB）
        /// <para>大于等于此值的缓冲区归为大型池</para>
        /// </summary>
        public const int LargeThreshold = 1024 * 1024;

        #endregion

        #region Private Fields

        /// <summary>
        /// 小型缓冲区池，使用共享池
        /// </summary>
        private static readonly ArrayPool<byte> _smallPool = ArrayPool<byte>.Shared;

        /// <summary>
        /// 中型缓冲区池，最大数组长度64KB，最大保留100个数组
        /// </summary>
        private static readonly ArrayPool<byte> _mediumPool = ArrayPool<byte>.Create(MediumThreshold, 100);

        /// <summary>
        /// 大型缓冲区池，最大数组长度1MB，最大保留50个数组
        /// </summary>
        private static readonly ArrayPool<byte> _largePool = ArrayPool<byte>.Create(LargeThreshold, 50);

        /// <summary>
        /// 小型池租用计数
        /// </summary>
        private static long _smallPoolRented;

        /// <summary>
        /// 小型池归还计数
        /// </summary>
        private static long _smallPoolReturned;

        /// <summary>
        /// 中型池租用计数
        /// </summary>
        private static long _mediumPoolRented;

        /// <summary>
        /// 中型池归还计数
        /// </summary>
        private static long _mediumPoolReturned;

        /// <summary>
        /// 大型池租用计数
        /// </summary>
        private static long _largePoolRented;

        /// <summary>
        /// 大型池归还计数
        /// </summary>
        private static long _largePoolReturned;

        #endregion

        #region Public Methods

        /// <summary>
        /// 根据缓冲区大小获取对应的分级
        /// </summary>
        /// <param name="size">缓冲区大小（字节）</param>
        /// <returns>缓冲区大小分级</returns>
        public static BufferSizeTier GetTier(int size)
        {
            if (size < 0)
                return BufferSizeTier.Small;

            if (size < SmallThreshold)
                return BufferSizeTier.Small;

            if (size < MediumThreshold)
                return BufferSizeTier.Medium;

            return BufferSizeTier.Large;
        }

        /// <summary>
        /// 从内存池租用指定大小的字节数组
        /// </summary>
        /// <param name="size">需要租用的缓冲区最小大小（字节）</param>
        /// <returns>租用的字节数组，实际长度可能大于请求的大小</returns>
        /// <remarks>
        /// 返回的数组必须调用 <see cref="Return(byte[], int)"/> 归还到池中，否则会导致内存泄漏
        /// </remarks>
        public static byte[] Rent(int size)
        {
            if (size <= 0)
                size = SmallThreshold;

            var tier = GetTier(size);
            byte[] buffer;

            switch (tier)
            {
                case BufferSizeTier.Small:
                    buffer = _smallPool.Rent(size);
                    Interlocked.Increment(ref _smallPoolRented);
                    break;

                case BufferSizeTier.Medium:
                    buffer = _mediumPool.Rent(size);
                    Interlocked.Increment(ref _mediumPoolRented);
                    break;

                case BufferSizeTier.Large:
                    buffer = _largePool.Rent(size);
                    Interlocked.Increment(ref _largePoolRented);
                    break;

                default:
                    buffer = _smallPool.Rent(size);
                    Interlocked.Increment(ref _smallPoolRented);
                    break;
            }

            return buffer;
        }

        /// <summary>
        /// 将字节数组归还到内存池
        /// </summary>
        /// <param name="buffer">要归还的字节数组</param>
        /// <param name="originalSize">
        /// 原始请求的大小，用于确定归还到哪个池。
        /// 如果为 -1（默认值），则尝试根据缓冲区长度自动确定池
        /// </param>
        /// <remarks>
        /// 归还后不应再使用此数组，池可能会将其重新分配给其他租用者
        /// </remarks>
        public static void Return(byte[] buffer, int originalSize = -1)
        {
            if (buffer == null)
                return;

            BufferSizeTier tier;

            if (originalSize < 0)
            {
                // 根据缓冲区容量自动确定池
                tier = GetTier(buffer.Length);
            }
            else
            {
                tier = GetTier(originalSize);
            }

            switch (tier)
            {
                case BufferSizeTier.Small:
                    _smallPool.Return(buffer);
                    Interlocked.Increment(ref _smallPoolReturned);
                    break;

                case BufferSizeTier.Medium:
                    _mediumPool.Return(buffer);
                    Interlocked.Increment(ref _mediumPoolReturned);
                    break;

                case BufferSizeTier.Large:
                    _largePool.Return(buffer);
                    Interlocked.Increment(ref _largePoolReturned);
                    break;

                default:
                    _smallPool.Return(buffer);
                    Interlocked.Increment(ref _smallPoolReturned);
                    break;
            }
        }

        /// <summary>
        /// 从内存池租用指定大小的池化缓冲区
        /// </summary>
        /// <param name="size">需要租用的缓冲区大小（字节）</param>
        /// <returns>池化缓冲区对象，使用 using 语句可自动归还到池中</returns>
        /// <remarks>
        /// 返回的 <see cref="PooledBuffer"/> 对象实现了 <see cref="IDisposable"/> 接口，
        /// 使用 using 语句可确保缓冲区自动归还到池中
        /// </remarks>
        public static PooledBuffer RentPooled(int size)
        {
            if (size <= 0)
                size = SmallThreshold;

            var tier = GetTier(size);
            ArrayPool<byte> pool;
            byte[] buffer;

            switch (tier)
            {
                case BufferSizeTier.Small:
                    pool = _smallPool;
                    buffer = _smallPool.Rent(size);
                    Interlocked.Increment(ref _smallPoolRented);
                    break;

                case BufferSizeTier.Medium:
                    pool = _mediumPool;
                    buffer = _mediumPool.Rent(size);
                    Interlocked.Increment(ref _mediumPoolRented);
                    break;

                case BufferSizeTier.Large:
                    pool = _largePool;
                    buffer = _largePool.Rent(size);
                    Interlocked.Increment(ref _largePoolRented);
                    break;

                default:
                    pool = _smallPool;
                    buffer = _smallPool.Rent(size);
                    Interlocked.Increment(ref _smallPoolRented);
                    break;
            }

            return new PooledBuffer(buffer, size, tier, pool);
        }

        /// <summary>
        /// 获取内存池的当前统计信息
        /// </summary>
        /// <returns>包含租用、归还和活动缓冲区数量的统计信息</returns>
        public static MemoryPoolStatistics GetStatistics()
        {
            return new MemoryPoolStatistics
            {
                SmallPoolRented = _smallPoolRented,
                SmallPoolReturned = _smallPoolReturned,
                MediumPoolRented = _mediumPoolRented,
                MediumPoolReturned = _mediumPoolReturned,
                LargePoolRented = _largePoolRented,
                LargePoolReturned = _largePoolReturned
            };
        }

        #endregion
    }
}
