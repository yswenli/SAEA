/****************************************************************************
*项目名称：SAEA.Http2.Interfaces
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Interfaces
*类 名 称：IStream
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:18:05
*描述：
*=====================================================================
*修改时间：2019/6/27 16:18:05
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.Http2.Interfaces
{
    /// <summary>
    /// A HTTP/2 stream
    /// </summary>
    public interface IStream
        : IReadableByteStream, IWriteAndCloseableByteStream, IDisposable
    {
        uint Id { get; }

        StreamState State { get; }

        void Cancel();

        Task<IEnumerable<HeaderField>> ReadHeadersAsync();

        Task<IEnumerable<HeaderField>> ReadTrailersAsync();

        Task WriteHeadersAsync(IEnumerable<HeaderField> headers, bool endOfStream);

        Task WriteTrailersAsync(IEnumerable<HeaderField> headers);

        Task WriteAsync(ArraySegment<byte> buffer, bool endOfStream = false);
    }

}
