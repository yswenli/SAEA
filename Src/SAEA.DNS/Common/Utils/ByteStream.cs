/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| _f 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
   ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ _f 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.DNS.Common.Utils
*文件名： ByteStream
*版本号： v26.4.23.1
*唯一标识：22237e5c-399b-4010-ac18-f7f8c48446c0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/11/29 20:45:22
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.Common.Caching;
using System;
using System.Collections.Generic;
using System.IO;

namespace SAEA.DNS.Common.Utils
{
    public class ByteStream : Stream
    {
        private byte[] buffer;
        private int offset = 0;
        private bool _usePool = false;
        private int _capacity = 0;

        public ByteStream(int capacity)
        {
            // Use MemoryPoolManager for small to medium capacity
            if (capacity <= MemoryPoolManager.MediumThreshold)
            {
                buffer = MemoryPoolManager.Rent(capacity);
                _usePool = true;
            }
            else
            {
                buffer = new byte[capacity];
            }
            _capacity = capacity;
        }

        /// <summary>
        /// Releases resources and returns buffer to pool if applicable
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_usePool && buffer != null)
                {
                    MemoryPoolManager.Return(buffer, _capacity);
                    buffer = null;
                }
            }
            base.Dispose(disposing);
        }

        public ByteStream Append(IEnumerable<byte[]> buffers)
        {
            foreach (byte[] buf in buffers)
            {
                Write(buf, 0, buf.Length);
            }

            return this;
        }

        public ByteStream Append(byte[] buf)
        {
            Write(buf, 0, buf.Length);
            return this;
        }

        public byte[] ToArray()
        {
            byte[] result = new byte[offset];
            if (offset > 0)
            {
                Array.Copy(buffer, 0, result, 0, offset);
            }
            return result;
        }

        public void Reset()
        {
            this.offset = 0;
        }

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return buffer.Length > 0 && offset < buffer.Length; }
        }

        public override void Flush() { }

        public override long Length
        {
            get { return offset; }
        }

        public override long Position
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Array.Copy(buffer, offset, this.buffer, this.offset, count);
            this.offset += count;
        }
    }
}
