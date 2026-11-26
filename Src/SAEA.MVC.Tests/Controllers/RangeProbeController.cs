/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.MVC.Tests.Controllers
*文件名： RangeProbeController
*版本号： net10.0
*唯一标识：623bb736-729e-429e-a538-4c105c96bbfd
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/26 10:10:00
*描述：控制器返回内存文件结果供 Range 用例使用
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 17:46:21
*修改人： yswenli
*版本号： net10.0
*描述：新增带下载文件名的文件结果与大数据结果入口
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;

using SAEA.MVC.Controllers;
using SAEA.MVC.Results;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// Range 用例的控制器：返回内存中的 64 字节文件结果。
    /// </summary>
    public class RangeProbeController : Controller
    {
        /// <summary>
        /// 返回 64 字节文件结果，第 i 个字节的值为 i。
        /// </summary>
        /// <returns>文件结果</returns>
        public ActionResult FilePayload()
        {
            var payload = new byte[64];

            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)i;
            }

            return new FileResult(new MemoryStream(payload), "application/octet-stream");
        }

        /// <summary>
        /// 返回状态码 404 的大数据结果，用于验证非 200 不参与 Range 改写。
        /// </summary>
        /// <returns>大数据结果</returns>
        public ActionResult MissingBigData()
        {
            return new BigDataResult(new MemoryStream(BuildPayload(256)), "application/octet-stream", HttpStatusCode.NotFound);
        }

        /// <summary>
        /// 返回不可定位数据流的大数据结果，用于验证短连接与无长度正文。
        /// </summary>
        /// <returns>大数据结果</returns>
        public ActionResult NonSeekable()
        {
            return new BigDataResult(new NonSeekableReadStream(BuildPayload(4096)), "application/octet-stream");
        }

        /// <summary>
        /// 以 leaveOpen 为 true 返回数据流，交由调用方自行释放。
        /// </summary>
        /// <param name="id">数据流标识，供后续查询释放状态</param>
        /// <returns>大数据结果</returns>
        public ActionResult LeaveOpen(string id)
        {
            return new BigDataResult(StreamProbeRegistry.Create(id), "application/octet-stream", HttpStatusCode.OK, true);
        }

        /// <summary>
        /// 以默认 leaveOpen 返回数据流，由结果负责释放。
        /// </summary>
        /// <param name="id">数据流标识，供后续查询释放状态</param>
        /// <returns>大数据结果</returns>
        public ActionResult Owned(string id)
        {
            return new BigDataResult(StreamProbeRegistry.Create(id), "application/octet-stream");
        }

        /// <summary>
        /// 查询指定标识的数据流是否已被释放。
        /// </summary>
        /// <param name="id">数据流标识</param>
        /// <returns>disposed 或 open</returns>
        public string StreamState(string id)
        {
            return StreamProbeRegistry.IsDisposed(id) ? "disposed" : "open";
        }

        /// <summary>
        /// 返回带下载文件名的 64 字节文件结果，用于验证 Content-Disposition。
        /// </summary>
        /// <returns>文件结果</returns>
        public ActionResult Attachment()
        {
            return new FileResult(new MemoryStream(BuildPayload(64)), "application/octet-stream", HttpStatusCode.OK, "报告.txt");
        }

        /// <summary>
        /// 返回带下载文件名的大数据结果，用于验证流式路径的 Content-Disposition。
        /// </summary>
        /// <returns>大数据结果</returns>
        public ActionResult BigDataAttachment()
        {
            return new BigDataResult(new MemoryStream(BuildPayload(64)), "application/octet-stream", HttpStatusCode.OK, false, "报告.bin");
        }

        /// <summary>
        /// 生成第 i 个字节值为 i % 251 的正文。
        /// </summary>
        /// <param name="length">正文长度</param>
        /// <returns>按规则填充的字节数组</returns>
        private static byte[] BuildPayload(int length)
        {
            var payload = new byte[length];

            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % 251);
            }

            return payload;
        }

        /// <summary>
        /// 按标识保存 create 出的数据流，供后续请求查询释放状态。
        /// </summary>
        private static class StreamProbeRegistry
        {
            private static readonly ConcurrentDictionary<string, TrackedStream> Streams = new ConcurrentDictionary<string, TrackedStream>();

            /// <summary>
            /// 创建并保存一个 64 字节数据流。
            /// </summary>
            /// <param name="id">数据流标识</param>
            /// <returns>新建的数据流</returns>
            internal static Stream Create(string id)
            {
                var stream = new TrackedStream(BuildPayload(64));

                Streams[id ?? string.Empty] = stream;

                return stream;
            }

            /// <summary>
            /// 查询指定标识的数据流是否已被释放。
            /// </summary>
            /// <param name="id">数据流标识</param>
            /// <returns>已释放返回 true</returns>
            internal static bool IsDisposed(string id)
            {
                TrackedStream stream;

                return Streams.TryGetValue(id ?? string.Empty, out stream) && stream.IsDisposed;
            }
        }

        /// <summary>
        /// 记录是否被释放的内存流，用于验证 leaveOpen 的语义。
        /// </summary>
        private sealed class TrackedStream : MemoryStream
        {
            /// <summary>
            /// 以一段正文创建可定位的只读内存流。
            /// </summary>
            /// <param name="payload">正文</param>
            public TrackedStream(byte[] payload) : base(payload, false)
            {
            }

            /// <summary>
            /// 是否已经被释放。
            /// </summary>
            public bool IsDisposed { get; private set; }

            /// <summary>
            /// 记录释放状态后交给基类释放。
            /// </summary>
            /// <param name="disposing">是否为显式释放</param>
            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;

                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// 不可定位只读流：Length、Position 与 Seek 均不支持。
        /// </summary>
        private sealed class NonSeekableReadStream : Stream
        {
            private readonly MemoryStream _inner;

            /// <summary>
            /// 包装一段内存正文。
            /// </summary>
            /// <param name="payload">正文</param>
            public NonSeekableReadStream(byte[] payload)
            {
                _inner = new MemoryStream(payload, false);
            }

            /// <summary>
            /// 是否可读。
            /// </summary>
            public override bool CanRead { get { return true; } }

            /// <summary>
            /// 是否可定位，固定为 false。
            /// </summary>
            public override bool CanSeek { get { return false; } }

            /// <summary>
            /// 是否可写，固定为 false。
            /// </summary>
            public override bool CanWrite { get { return false; } }

            /// <summary>
            /// 长度不可用。
            /// </summary>
            public override long Length { get { throw new NotSupportedException(); } }

            /// <summary>
            /// 位置不可用。
            /// </summary>
            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            /// <summary>
            /// 不缓冲，无需刷新。
            /// </summary>
            public override void Flush()
            {
            }

            /// <summary>
            /// 读取正文。
            /// </summary>
            /// <param name="buffer">目标缓冲</param>
            /// <param name="offset">起始下标</param>
            /// <param name="count">读取字节数</param>
            /// <returns>实际读取的字节数</returns>
            public override int Read(byte[] buffer, int offset, int count)
            {
                return _inner.Read(buffer, offset, count);
            }

            /// <summary>
            /// 不支持定位。
            /// </summary>
            /// <param name="offset">偏移量</param>
            /// <param name="origin">基准位置</param>
            /// <returns>不会返回</returns>
            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            /// <summary>
            /// 不支持修改长度。
            /// </summary>
            /// <param name="value">新长度</param>
            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            /// <summary>
            /// 不支持写入。
            /// </summary>
            /// <param name="buffer">源缓冲</param>
            /// <param name="offset">起始下标</param>
            /// <param name="count">写入字节数</param>
            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            /// <summary>
            /// 释放内部流。
            /// </summary>
            /// <param name="disposing">是否为显式释放</param>
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    _inner.Dispose();

                base.Dispose(disposing);
            }
        }
    }
}