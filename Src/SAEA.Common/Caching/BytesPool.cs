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
    /// SmartArrayPool
    /// </summary>
    public class BytesPool : IDisposable
    {
        static ArrayPool<byte> _pool;

        /// <summary>
        /// SmartArrayPool
        /// </summary>
        static BytesPool()
        {
            _pool = ArrayPool<byte>.Shared;
        }

        byte[] _buffer;

        int _minLength = 0;

        /// <summary>
        /// SmartArrayPool
        /// </summary>
        /// <param name="minLength"></param>
        public BytesPool(int minLength)
        {
            _minLength = minLength;
            _buffer = _pool.Rent(minLength);
        }

        /// <summary>
        /// GetBuffer
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public ReadOnlyMemory<byte> GetBuffer(byte[] data, int offset)
        {
            Buffer.BlockCopy(data, offset, _buffer, 0, _minLength);
            return _buffer.AsMemory().Slice(0, _minLength);
        }
        /// <summary>
        /// GetBuffer
        /// </summary>
        public void Dispose()
        {
            _pool.Return(_buffer);
        }
    }
}
