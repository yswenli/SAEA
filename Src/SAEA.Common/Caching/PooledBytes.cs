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
*文件名： PooledBytes
*版本号： v26.4.23.1
*唯一标识：c0e909ee-b985-4750-9e80-3024a1e2b800
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2023/09/26 19:09:21
*描述：PooledBytes类
*
*=====================================================================
*修改标记
*修改时间：2023/09/26 19:09:21
*修改人： yswenli
*版本号： v26.4.23.1
*描述：PooledBytes类
*
*****************************************************************************/
using System;
using System.Buffers;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 池化字节数组
    /// </summary>
    public class PooledBytes : IDisposable
    {
        static ArrayPool<byte> _pool;

        /// <summary>
        /// SmartArrayPool
        /// </summary>
        static PooledBytes()
        {
            _pool = ArrayPool<byte>.Shared;
        }

        /// <summary>
        /// 字节数组
        /// </summary>
        public byte[] Bytes { get; private set; }


        /// <summary>
        /// SmartArrayPool
        /// </summary>
        /// <param name="minLength"></param>
        public PooledBytes(int minLength)
        {
            Bytes = _pool.Rent(minLength);
        }

        /// <summary>
        /// 复制
        /// </summary>
        /// <param name="source"></param>
        /// <param name="startIndex"></param>
        /// <param name="count"></param>
        public void BlockCopy(byte[] source, int startIndex, int count)
        {
            Buffer.BlockCopy(source, startIndex, Bytes, 0, count);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Free()
        {
            _pool.Return(Bytes);
        }

        /// <summary>
        /// GetBuffer
        /// </summary>
        public void Dispose()
        {
            Free();
        }
    }
}
