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
*描述：DNS 字节流读写工具
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:47
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common.Caching;
using System;
using System.Collections.Generic;
using System.IO;

namespace SAEA.DNS.Common.Utils
{
    /// <summary>
    /// 只写字节流，按需追加并在释放时归还池化缓冲
    /// </summary>
    public class ByteStream : Stream
    {
        private byte[] buffer;
        private int offset = 0;
        private bool _usePool = false;
        private int _capacity = 0;

        /// <summary>
        /// 按预估容量创建字节流
        /// </summary>
        /// <param name="capacity">初始缓冲容量（字节），不超过内存池阈值时从池中租用</param>
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
        /// 释放资源，池化缓冲在此归还内存池
        /// </summary>
        /// <param name="disposing">true 表示由 Dispose 调用而非终结器调用</param>
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

        /// <summary>
        /// 依次追加多个字节数组
        /// </summary>
        /// <param name="buffers">待追加的字节数组集合</param>
        /// <returns>当前实例，便于链式调用</returns>
        public ByteStream Append(IEnumerable<byte[]> buffers)
        {
            foreach (byte[] buf in buffers)
            {
                Write(buf, 0, buf.Length);
            }

            return this;
        }

        /// <summary>
        /// 追加一个字节数组
        /// </summary>
        /// <param name="buf">待追加的字节数组</param>
        /// <returns>当前实例，便于链式调用</returns>
        public ByteStream Append(byte[] buf)
        {
            Write(buf, 0, buf.Length);
            return this;
        }

        /// <summary>
        /// 把已写入的数据复制为等长新数组
        /// </summary>
        /// <returns>仅包含已写入部分的副本</returns>
        public byte[] ToArray()
        {
            byte[] result = new byte[offset];
            if (offset > 0)
            {
                Array.Copy(buffer, 0, result, 0, offset);
            }
            return result;
        }

        /// <summary>
        /// 把写入位置归零以便复用缓冲，不清除已有内容
        /// </summary>
        public void Reset()
        {
            this.offset = 0;
        }

        /// <summary>
        /// 始终为 false，本类不支持读取
        /// </summary>
        public override bool CanRead
        {
            get { return false; }
        }

        /// <summary>
        /// 始终为 false，本类不支持定位
        /// </summary>
        public override bool CanSeek
        {
            get { return false; }
        }

        /// <summary>
        /// 缓冲区非空且仍有剩余空间时为 true
        /// </summary>
        public override bool CanWrite
        {
            get { return buffer.Length > 0 && offset < buffer.Length; }
        }

        /// <summary>
        /// 空实现，数据直接写入内部数组无需刷新
        /// </summary>
        public override void Flush() { }

        /// <summary>
        /// 已写入的字节数
        /// </summary>
        public override long Length
        {
            get { return offset; }
        }

        /// <summary>
        /// 不支持，读写该属性都会抛出 NotImplementedException
        /// </summary>
        public override long Position
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        /// <summary>
        /// 不支持读取，调用即抛出 NotImplementedException
        /// </summary>
        /// <param name="buffer">目标缓冲，未使用</param>
        /// <param name="offset">起始偏移，未使用</param>
        /// <param name="count">读取字节数，未使用</param>
        /// <returns>不会返回</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 不支持定位，调用即抛出 NotImplementedException
        /// </summary>
        /// <param name="offset">目标偏移，未使用</param>
        /// <param name="origin">定位基准，未使用</param>
        /// <returns>不会返回</returns>
        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 不支持修改长度，调用即抛出 NotImplementedException
        /// </summary>
        /// <param name="value">目标长度，未使用</param>
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 把源数组指定范围的数据追加到缓冲末尾，容量不足时由数组复制抛出异常
        /// </summary>
        /// <param name="buffer">源字节数组</param>
        /// <param name="offset">源数组起始偏移</param>
        /// <param name="count">追加的字节数</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            Array.Copy(buffer, offset, this.buffer, this.offset, count);
            this.offset += count;
        }
    }
}
