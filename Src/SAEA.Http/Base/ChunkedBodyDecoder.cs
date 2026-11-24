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
*文件名： ChunkedBodyDecoder
*版本号： v26.11.24.1
*唯一标识：643ad41c-f360-43c2-9a91-54d8fdf36766
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/24 16:14:52
*描述：把分帧器产出的原始 chunked 负载解码成请求体流，优先原地解码，超阈值写临时文件
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 18:12:35
*修改人： yswenli
*版本号： 
*描述：v26.11.24.2
*
*****************************************************************************/
using System;
using System.IO;
using System.Runtime.InteropServices;

using SAEA.Common;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http.Base
{
    /// <summary>
    /// chunked 负载解码器：按 chunk 语法还原负载字节，chunk 扩展与 trailer 只跳过不解析。
    /// <para>
    /// 单遍解码：逐块把数据搬到已消费的 chunk 头位置，读指针始终不小于写指针，因此不额外分配负载数组；
    /// 解码总长越过落盘阈值时，已解码前缀与后续块直接写入临时文件，不再回头重扫原始帧。
    /// </para>
    /// <para>
    /// 结构不符按 <see cref="HttpProtocolErrorCodes.BadRequest"/> 抛 <see cref="FrameProtocolException"/>；
    /// 分帧器已校验终止块与长度上限，这里再做一次结构与长度校验，防止绕过分帧器的调用方传入半截负载。
    /// </para>
    /// </summary>
    internal static class ChunkedBodyDecoder
    {
        // 与分帧器一致：chunk-size 最多 8 位十六进制，超出即畸形
        const int MaxChunkSizeDigits = 8;

        /// <summary>
        /// 解码 chunked 负载。
        /// </summary>
        /// <param name="rawBody">分帧器产出的原始 chunked 负载，即帧内报头之后的部分</param>
        /// <param name="largeBodyThreshold">落盘阈值，单位字节；0 表示始终留在内存</param>
        /// <param name="tempDirectory">临时文件目录，null 或空白时用系统临时目录下的 saea-http</param>
        /// <param name="channelId">连接标识，仅用于临时文件命名</param>
        /// <param name="frameOwner">原始帧所有者，解码结果仍引用帧字节时由流接管释放，不复用时由本方法释放</param>
        /// <param name="decodedLength">解码后的负载长度，单位字节</param>
        /// <returns>只读且可 Seek 的请求体流</returns>
        internal static Stream Decode(
            ReadOnlyMemory<byte> rawBody,
            int largeBodyThreshold,
            string tempDirectory,
            string channelId,
            IDisposable frameOwner,
            out int decodedLength)
        {
            if (!MemoryMarshal.TryGetArray(rawBody, out var segment) || segment.Array == null)
            {
                // 无数组背衬时无法原地写回：先复制成独立数组再走同一条解码路径，只保留一份解码逻辑。
                var copy = rawBody.ToArray();

                // 结果不再引用原始帧，因此把 owner 留到解码成功后再释放：
                // 解码失败时由调用方统一 Release，否则同一次 Retain 会在异常路径上被释放两次。
                var decoded = DecodeCore(
                    new ReadOnlyMemory<byte>(copy), copy, 0,
                    largeBodyThreshold, tempDirectory, channelId, null, out decodedLength);

                ReleaseOwner(frameOwner);

                return decoded;
            }

            return DecodeCore(
                rawBody, segment.Array, segment.Offset,
                largeBodyThreshold, tempDirectory, channelId, frameOwner, out decodedLength);
        }

        /// <summary>
        /// 单遍解码：边读 chunk 头边把数据写到已消费区间，同时累计长度；越回落盘阈值时把已解码前缀与后续块直接写入临时文件。
        /// <para>
        /// 写指针始终落后于读指针，因此原地解码不会覆盖待读字节；只有携带数组背衬的输入才走原地路径。
        /// </para>
        /// </summary>
        /// <param name="frame">原始 chunked 负载，其内存可被原地写入</param>
        /// <param name="array">帧的背衬数组</param>
        /// <param name="baseOffset">负载在背衬数组中的起始偏移</param>
        /// <param name="largeBodyThreshold">落盘阈值，单位字节；0 表示始终留在内存</param>
        /// <param name="tempDirectory">临时文件目录</param>
        /// <param name="channelId">连接标识</param>
        /// <param name="frameOwner">原始帧所有者，落盘后不再需要，写完即释放</param>
        /// <param name="decodedLength">解码后的负载长度，单位字节</param>
        /// <returns>只读且可 Seek 的请求体流</returns>
        static Stream DecodeCore(
            ReadOnlyMemory<byte> frame,
            byte[] array,
            int baseOffset,
            int largeBodyThreshold,
            string tempDirectory,
            string channelId,
            IDisposable frameOwner,
            out int decodedLength)
        {
            var span = frame.Span;
            var read = 0;
            var written = 0;

            FileStream file = null;
            string filePath = null;

            try
            {
                while (true)
                {
                    var size = ReadChunkSize(span, ref read);

                    if (size == 0)
                    {
                        SkipTrailers(span, ref read);

                        break;
                    }

                    if (size > span.Length - read)
                        throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

                    if (file == null && largeBodyThreshold > 0 && (long)written + size > largeBodyThreshold)
                    {
                        // 落盘阈值在块边界被越过：已解码前缀还在帧内，先整体写入文件，后面的块直接追加。
                        file = HttpBodyStorage.CreateTempFile(tempDirectory, channelId, out filePath);

                        if (written > 0)
                            file.Write(array, baseOffset, written);
                    }

                    if (file != null)
                    {
                        // netstandard2.0 的 Stream 没有 Span 版 Write，只能按数组写入
                        file.Write(array, baseOffset + read, size);
                    }
                    else
                    {
                        // 每个 chunk 至少有 3 字节 size 行与 2 字节行尾，写指针始终落后于读指针
                        span.Slice(read, size).CopyTo(new Span<byte>(array, baseOffset + written, size));
                    }

                    read += size;
                    written += size;

                    ExpectCrLf(span, ref read);
                }

                decodedLength = written;

                if (file != null)
                {
                    file.Position = 0;

                    var result = HttpRequestBodyStream.FromFile(file, filePath);

                    file = null;

                    ReleaseOwner(frameOwner);

                    return result;
                }

                return HttpRequestBodyStream.FromMemory(frame.Slice(0, written), frameOwner);
            }
            catch
            {
                // 畸形 chunk 可能在临时文件创建后才被发现：必须清理，否则会留下垃圾文件。
                if (file != null)
                {
                    try
                    {
                        file.Dispose();
                    }
                    catch
                    {
                        // 原始异常更重要，关闭失败的次生异常直接忽略
                    }
                }

                HttpBodyStorage.TryDelete(filePath);

                throw;
            }
        }

        /// <summary>读一行 chunk-size，忽略扩展内容，返回数值。</summary>
        /// <param name="span">原始 chunked 负载</param>
        /// <param name="read">当前读偏移，成功后移到该行之后</param>
        /// <returns>chunk 数据长度，0 表示终止块</returns>
        static int ReadChunkSize(ReadOnlySpan<byte> span, ref int read)
        {
            var digits = 0;
            long value = 0;

            while (read < span.Length)
            {
                var current = span[read];

                var digit = ToHexValue(current);

                if (digit >= 0)
                {
                    digits++;

                    if (digits > MaxChunkSizeDigits)
                        throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

                    value = value * 16 + digit;

                    if (value > int.MaxValue)
                        throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

                    read++;
                    continue;
                }

                if (digits == 0)
                    throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

                if (current == 59)
                {
                    // chunk-ext 忽略，但同样要以 CRLF 收尾
                    read++;

                    while (read < span.Length && span[read] != 13)
                    {
                        if (span[read] == 10)
                            throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

                        read++;
                    }
                }

                break;
            }

            if (digits == 0)
                throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

            ExpectCrLf(span, ref read);

            return (int)value;
        }

        /// <summary>逐行丢弃 trailer，直到空行。</summary>
        /// <param name="span">原始 chunked 负载</param>
        /// <param name="read">当前读偏移，成功后移到终止空行之后</param>
        static void SkipTrailers(ReadOnlySpan<byte> span, ref int read)
        {
            while (true)
            {
                if (read + 1 < span.Length && span[read] == 13 && span[read + 1] == 10)
                {
                    read += 2;

                    return;
                }

                var lineEnd = span.Slice(read).IndexOf((byte)13);

                if (lineEnd < 0)
                    throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

                // 行内不允许出现裸 LF，否则行尾不是 CRLF
                if (span.Slice(read, lineEnd).IndexOf((byte)10) >= 0)
                    throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

                read += lineEnd;

                ExpectCrLf(span, ref read);
            }
        }

        /// <summary>校验并消费一个 CRLF。</summary>
        /// <param name="span">原始 chunked 负载</param>
        /// <param name="read">当前读偏移，成功后前移 2 字节</param>
        static void ExpectCrLf(ReadOnlySpan<byte> span, ref int read)
        {
            if (read + 1 >= span.Length || span[read] != 13 || span[read + 1] != 10)
                throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

            read += 2;
        }

        /// <summary>释放原始帧引用：落盘与精确分配后不再需要帧字节。</summary>
        /// <param name="frameOwner">原始帧所有者，可为 null</param>
        static void ReleaseOwner(IDisposable frameOwner)
        {
            if (frameOwner == null) return;

            try
            {
                frameOwner.Dispose();
            }
            catch (Exception ex)
            {
                // 归还失败只影响缓冲复用，不改变已成功解码的结果
                LogHelper.Error("ChunkedBodyDecoder.ReleaseOwner", ex);
            }
        }

        /// <summary>把十六进制 ASCII 转成数值，非十六进制字符返回 -1。</summary>
        /// <param name="value">待转换的字节</param>
        /// <returns>0-15 的数值，或 -1</returns>
        static int ToHexValue(byte value)
        {
            if (value >= 48 && value <= 57) return value - 48;
            if (value >= 65 && value <= 70) return value - 55;
            if (value >= 97 && value <= 102) return value - 87;

            return -1;
        }
    }
}