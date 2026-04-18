/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS.Common.Utils
*类 名 称：ByteStream
*版 本 号：v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/11/28 22:43:28
*描述：
*=====================================================================
*修改时间：2019/11/28 22:43:28
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
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
            return buffer;
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
