/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Common.Caching
*类 名 称：SmartArrayPool
*版本号： v7.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/16 9:43:28
*描述：
*=====================================================================
*修改时间：2019/1/16 9:43:28
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
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
