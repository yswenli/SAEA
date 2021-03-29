/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Common.Caching
*类 名 称：SmartArrayPool
*版 本 号： v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/16 9:43:28
*描述：
*=====================================================================
*修改时间：2019/1/16 9:43:28
*修 改 人： yswenli
*版 本 号： v5.0.0.1
*描    述：
*****************************************************************************/
using System;
using System.Buffers;

namespace SAEA.Common.Caching
{
    public class SmartArrayPool : IDisposable
    {
        ArrayPool<byte> _pool = ArrayPool<byte>.Shared;

        byte[] _buffer;

        int _minLength = 0;



        public SmartArrayPool(int minLength)
        {
            _minLength = minLength;
            _buffer = _pool.Rent(minLength);
        }

        public SmartArrayPool(int minLength, Action<byte[]> onShared) : this(minLength)
        {
            onShared(_buffer);
        }

        public void Share(Action<byte[]> onShared)
        {
            onShared(_buffer);
        }

        public void Share(byte[] data, Action<byte[]> onShared)
        {
            Buffer.BlockCopy(data, 0, _buffer, 0, _minLength);
        }

        public void Dispose()
        {
            _pool.Return(_buffer);
        }
    }
}
