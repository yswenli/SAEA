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
*命名空间：SAEA.Http.Base
*文件名： HttpRequestBodyStream
*版本号： v26.11.24.1
*唯一标识：5f0c7a2e-8f4b-4b7a-9d51-2c6e1f3a4b5d
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/24 15:41:00
*描述：只读请求体流，内存载体直接读取池化帧、文件载体读取临时文件
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 15:41:00
*修改人： yswenli
*版本号： v26.11.24.1
*描述：新建，请求体按阈值在内存与临时文件之间切换载体
*
*****************************************************************************/
using System;
using System.IO;
using System.Threading;

using SAEA.Common;

namespace SAEA.Http.Base
{
    /// <summary>
    /// 只读请求体流：内存载体直接引用池化帧，释放时归还整帧；文件载体读取临时文件，释放时删除文件。
    /// </summary>
    internal sealed class HttpRequestBodyStream : Stream
    {
        // 内存载体不复制字节，owner 是整帧的所有者（池化消息），释放流时才归还；
        // 文件载体持有已打开的临时文件，释放流时关闭并删除
        readonly ReadOnlyMemory<byte> _memory;

        readonly IDisposable _owner;

        readonly FileStream _fileStream;

        readonly string _filePath;

        int _ownerReleased;

        long _memoryPosition;

        bool _disposed;

        HttpRequestBodyStream(ReadOnlyMemory<byte> memory, IDisposable owner)
        {
            _memory = memory;
            _owner = owner;
        }

        HttpRequestBodyStream(FileStream fileStream, string filePath)
        {
            _fileStream = fileStream;
            _filePath = filePath;
        }

        /// <summary>
        /// 是否由临时文件承载
        /// </summary>
        public bool IsFileBacked
        {
            get { return _fileStream != null; }
        }

        /// <summary>
        /// 临时文件路径，内存载体为 null
        /// </summary>
        internal string TempFilePath
        {
            get { return _filePath; }
        }

        /// <summary>
        /// 创建内存载体的请求体流
        /// </summary>
        /// <param name="memory">请求体在池化帧中的切片</param>
        /// <param name="owner">整帧所有者，流释放时归还</param>
        /// <returns>请求体流</returns>
        public static HttpRequestBodyStream FromMemory(ReadOnlyMemory<byte> memory, IDisposable owner)
        {
            return new HttpRequestBodyStream(memory, owner);
        }

        /// <summary>
        /// 创建文件载体的请求体流
        /// </summary>
        /// <param name="fileStream">已打开的临时文件流</param>
        /// <param name="filePath">临时文件路径，释放时删除</param>
        /// <returns>请求体流</returns>
        public static HttpRequestBodyStream FromFile(FileStream fileStream, string filePath)
        {
            if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));

            return new HttpRequestBodyStream(fileStream, filePath);
        }

        /// <summary>
        /// 尝试取出内存载体的字节切片；文件载体或已释放时返回 false
        /// </summary>
        /// <param name="memory">请求体字节切片</param>
        /// <returns>内存载体可用时返回 true</returns>
        public bool TryGetMemory(out ReadOnlyMemory<byte> memory)
        {
            EnsureNotDisposed();

            if (_fileStream != null)
            {
                memory = default;

                return false;
            }

            memory = _memory;

            return true;
        }

        /// <inheritdoc />
        public override bool CanRead
        {
            get { return !_disposed; }
        }

        /// <inheritdoc />
        public override bool CanSeek
        {
            get { return !_disposed; }
        }

        /// <inheritdoc />
        public override bool CanWrite
        {
            get { return false; }
        }

        /// <inheritdoc />
        public override long Length
        {
            get
            {
                EnsureNotDisposed();

                return _fileStream != null ? _fileStream.Length : _memory.Length;
            }
        }

        /// <inheritdoc />
        public override long Position
        {
            get
            {
                EnsureNotDisposed();

                return _fileStream != null ? _fileStream.Position : _memoryPosition;
            }
            set
            {
                EnsureNotDisposed();

                if (_fileStream != null)
                {
                    _fileStream.Position = value;

                    return;
                }

                if (value < 0 || value > _memory.Length)
                    throw new IOException("请求体位置超出范围。");

                _memoryPosition = value;
            }
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            EnsureNotDisposed();

            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (_fileStream != null) return _fileStream.Read(buffer, offset, count);

            var remaining = _memory.Length - (int)_memoryPosition;

            if (remaining <= 0) return 0;

            var toRead = count < remaining ? count : remaining;

            _memory.Span.Slice((int)_memoryPosition, toRead).CopyTo(buffer.AsSpan(offset, toRead));

            _memoryPosition += toRead;

            return toRead;
        }

        /// <inheritdoc />
        public override int ReadByte()
        {
            EnsureNotDisposed();

            if (_fileStream != null) return _fileStream.ReadByte();

            if (_memoryPosition >= _memory.Length) return -1;

            return _memory.Span[(int)_memoryPosition++];
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            EnsureNotDisposed();

            if (_fileStream != null) return _fileStream.Seek(offset, origin);

            long target;

            switch (origin)
            {
                case SeekOrigin.Begin:
                    target = offset;
                    break;
                case SeekOrigin.Current:
                    target = _memoryPosition + offset;
                    break;
                case SeekOrigin.End:
                    target = _memory.Length + offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin));
            }

            if (target < 0 || target > _memory.Length)
                throw new IOException("请求体位置超出范围。");

            _memoryPosition = target;

            return target;
        }

        /// <inheritdoc />
        public override void Flush()
        {
            EnsureNotDisposed();

            if (_fileStream != null) _fileStream.Flush();
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new NotSupportedException("请求体流是只读的，不支持修改长度。");
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("请求体流是只读的，不支持写入。");
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                base.Dispose(disposing);

                return;
            }

            if (_disposed) return;

            _disposed = true;

            if (_fileStream != null)
            {
                try
                {
                    _fileStream.Dispose();
                }
                catch (Exception ex)
                {
                    // 关闭失败不影响后续删除，删除失败也只记日志
                    LogHelper.Error("HttpRequestBodyStream.Dispose", ex);
                }

                TryDeleteFile();
            }

            // 内存载体要让出整帧引用：只归还一次，重复 Dispose 不能重复归还
            if (Interlocked.Exchange(ref _ownerReleased, 1) == 0 && _owner != null)
            {
                try
                {
                    _owner.Dispose();
                }
                catch (Exception ex)
                {
                    LogHelper.Error("HttpRequestBodyStream.ReleaseFrame", ex);
                }
            }

            base.Dispose(disposing);
        }

        void EnsureNotDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(HttpRequestBodyStream));
        }

        void TryDeleteFile()
        {
            if (string.IsNullOrEmpty(_filePath)) return;

            try
            {
                if (File.Exists(_filePath)) File.Delete(_filePath);
            }
            catch (Exception ex)
            {
                // 临时文件删不掉只占磁盘，不向业务抛异常
                LogHelper.Error("HttpRequestBodyStream.DeleteTempFile", ex);
            }
        }
    }
}