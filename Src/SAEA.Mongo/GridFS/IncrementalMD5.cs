/****************************************************************************
*项目名称：SAEA.Mongo.GridFS
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Mongo.GridFS
*类 名 称：IncrementalMD5
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/5/24 13:27:42
*描述：
*=====================================================================
*修改时间：2019/5/24 13:27:42
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Security.Cryptography;

namespace SAEA.Mongo.GridFS
{
    internal abstract class IncrementalMD5 : IDisposable
    {
        public static IncrementalMD5 Create()
        {
#if NET452
            return new IncrementalMD5Net45();
#else
            return new IncrementalMD5NetStandard16();
#endif
        }

        public abstract void AppendData(byte[] data, int offset, int count);
        public abstract void Dispose();
        public abstract byte[] GetHashAndReset();
    }

#if NET452
    internal class IncrementalMD5Net45 : IncrementalMD5
    {
        private static readonly byte[] __emptyByteArray = new byte[0];

        private MD5 _md5;

        public override void AppendData(byte[] data, int offset, int count)
        {
            if (_md5 == null)
            {
                _md5 = MD5.Create();
            }
            _md5.TransformBlock(data, offset, count, null, 0);
        }

        public override void Dispose()
        {
            if (_md5 != null)
            {
                _md5.Dispose();
            }
        }

        public override byte[] GetHashAndReset()
        {
            if (_md5 == null)
            {
                _md5 = MD5.Create();
            }
            _md5.TransformFinalBlock(__emptyByteArray, 0, 0);
            var hash = _md5.Hash;
            _md5.Dispose();
            _md5 = null;
            return hash;
        }
    }
#else
    internal class IncrementalMD5NetStandard16 : IncrementalMD5
    {
        private readonly IncrementalHash _incrementalHash;

        public IncrementalMD5NetStandard16()
        {
            _incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        }

        public override void AppendData(byte[] data, int offset, int count)
        {
            _incrementalHash.AppendData(data, offset, count);
        }

        public override void Dispose()
        {
            _incrementalHash.Dispose();
        }

        public override byte[] GetHashAndReset()
        {
            return _incrementalHash.GetHashAndReset();
        }
    }
#endif
}
