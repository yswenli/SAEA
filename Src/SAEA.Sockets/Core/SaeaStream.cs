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
*命名空间：SAEA.Sockets.Core
*文件名： SaeaStream
*版本号： v26.11.5.1
*唯一标识：598e2a80-bb67-4eb1-af50-af4b5297457a
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 15:06:36
*描述：saea 连接的双工流门面
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:11:19
*修改人： yswenli
*版本号： v26.11.5.1
*描述：规范文件头 banner
*
*****************************************************************************/
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// saea 连接的双工流门面。读操作来自 <see cref="SaeaConnection"/> 的入站 Pipe，
    /// 写操作经 saea（TLS 时先加密）发送。
    /// </summary>
    public sealed class SaeaStream : Stream
    {
        private readonly SaeaConnection _connection;

        internal SaeaStream(SaeaConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => _connection.CanWrite;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _connection.ReadAsync(buffer, offset, count, cancellationToken);

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
            => WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        /// <inheritdoc />
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _connection.WriteAsync(buffer, offset, count, cancellationToken);

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _connection.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}