/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Model
*文件名： HttpBigDataResult
*版本号： v26.4.23.1
*唯一标识：74d2464d-0fd3-4962-944e-29f88e5510c9
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/05/20 14:37:39
*描述：HttpBigDataResult接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 17:46:21
*修改人： yswenli
*版本号： v26.11.26.1
*描述：实现 IAttachmentResult，新增带 fileName 的构造重载并在报头前写 Content-Disposition
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Http.Base;
using SAEA.Http.Common;
using System;
using System.IO;
using System.Net;
using System.Text;

namespace SAEA.Http.Model
{
    /// <summary>
    /// 大数据结果
    /// </summary>
    public class HttpBigDataResult : HttpActionResult, IBigDataResult, IAttachmentResult
    {
        /// <summary>
        /// 下载文件名；为 null 或空白表示不写 Content-Disposition
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// 大数据结果
        /// </summary>
        /// <param name="stream">正文数据流</param>
        /// <param name="contentType">响应内容类型</param>
        /// <param name="status">响应状态码</param>
        /// <param name="leaveOpen">发送结束后是否保留调用方传入的数据流不关闭，默认 false 表示由本结果负责关闭；仅对 Stream 构造生效，filePath 构造总是关闭内部打开的流</param>
        public HttpBigDataResult(Stream stream, string contentType = "", HttpStatusCode status = HttpStatusCode.OK, bool leaveOpen = false)
            : this(stream, contentType, status, leaveOpen, null)
        {

        }

        /// <summary>
        /// 大数据结果，并指定下载文件名
        /// </summary>
        /// <param name="stream">正文数据流</param>
        /// <param name="contentType">响应内容类型</param>
        /// <param name="status">响应状态码</param>
        /// <param name="leaveOpen">发送结束后是否保留调用方传入的数据流不关闭</param>
        /// <param name="fileName">下载文件名；为 null 或空白时不写 Content-Disposition</param>
        public HttpBigDataResult(Stream stream, string contentType, HttpStatusCode status, bool leaveOpen, string fileName)
        {
            long total = -1;

            this.ContentEncoding = Encoding.UTF8;
            this.ContentType = contentType;
            this.Status = status;
            this.FileName = fileName;

            try
            {
                total = stream.CanSeek ? stream.Length : -1;

                var response = HttpContext.Current.Response;

                string rangeHeader;

                HttpContext.Current.Request.Headers.TryGetValue("Range", out rangeHeader);

                // 只有 200 完整响应且长度已知的流才能按区间定位；非 200 与不可定位流按完整正文处理
                var parsed = status == HttpStatusCode.OK && stream.CanSeek && total >= 0
                    ? ByteRangeHeader.Parse(rangeHeader, total)
                    : default(ByteRangeParseResult);

                response.ContentType = contentType;

                var disposition = ContentDispositionHelper.Build(fileName);
                if (disposition != null)
                    response.Headers[ResponseHeaderType.ContentDisposition.GetDescription()] = disposition;

                response.Status = status;

                long remaining;

                if (parsed.Kind == ByteRangeParseKind.Satisfiable)
                {
                    response.Status = HttpStatusCode.PartialContent;
                    response.Headers["Accept-Ranges"] = "bytes";
                    response.Headers["Content-Range"] = "bytes " + parsed.Start + "-" + parsed.End + "/" + total;
                    response.SendHeader(parsed.Length);

                    stream.Position = parsed.Start;

                    remaining = parsed.Length;
                }
                else if (parsed.Kind == ByteRangeParseKind.Unsatisfiable)
                {
                    response.Status = HttpStatusCode.RequestedRangeNotSatisfiable;
                    response.Headers["Accept-Ranges"] = "bytes";
                    response.Headers["Content-Range"] = "bytes */" + total;
                    response.Headers["Content-Length"] = "0";
                    response.SendHeader(0);
                    response.SendEnd();

                    return;
                }
                else if (!stream.CanSeek)
                {
                    // 不可定位流没有总长度：不声明 Accept-Ranges，改用短连接通知客户端正文以连接关闭结束
                    response.Headers["Connection"] = "close";
                    response.SendHeader(-1);

                    remaining = -1;
                }
                else
                {
                    // 非 200 不声明区间支持，保持传入状态与完整正文
                    if (status == HttpStatusCode.OK)
                        response.Headers["Accept-Ranges"] = "bytes";

                    // 空正文也要有明确长度，否则分段写路径不会写 Content-Length
                    if (total == 0)
                        response.Headers["Content-Length"] = "0";

                    response.SendHeader(total);

                    stream.Position = 0;

                    remaining = -1;
                }

                // HEAD 只要报头：此时 Content-Length 已写出，立即收尾，不分配也不读取正文
                if (response.SuppressBody)
                {
                    response.SendEnd();

                    return;
                }

                const int bufferSize = 16384;

                var buffer = MemoryPoolManager.Rent(bufferSize);

                try
                {
                    while (remaining != 0)
                    {
                        // 区间请求只读剩余字节数，最后一块按剩余量收窄
                        var readSize = remaining < 0 ? bufferSize : (int)Math.Min(bufferSize, remaining);

                        var count = stream.Read(buffer, 0, readSize);

                        if (count < 1)
                        {
                            break;
                        }

                        // 池化数组可能比 16384 大，只能下发实际读到的字节
                        response.SendData(buffer, 0, count);

                        if (remaining > 0) remaining -= count;
                    }

                    response.SendEnd();
                }
                finally
                {
                    MemoryPoolManager.Return(buffer, bufferSize);
                }
            }
            finally
            {
                if (!leaveOpen)
                    CloseQuietly(stream);
            }
        }

        /// <summary>
        /// 关闭数据流，关闭失败不覆盖发送阶段抛出的原始异常
        /// </summary>
        /// <param name="stream">待关闭的数据流</param>
        static void CloseQuietly(Stream stream)
        {
            try
            {
                stream.Dispose();
            }
            catch
            {
                // 关闭失败通常来自已断开的底层连接，不能盖掉真正的发送异常
            }
        }


        /// <summary>
        /// 大数据结果
        /// </summary>
        /// <param name="filePath">文件路径，内部打开的流由本结果负责关闭</param>
        /// <param name="status">响应状态码</param>
        public HttpBigDataResult(string filePath, HttpStatusCode status = HttpStatusCode.OK)
            : this(filePath, null, status)
        {

        }

        /// <summary>
        /// 大数据结果，并指定下载文件名
        /// </summary>
        /// <param name="filePath">文件路径，内部打开的流由本结果负责关闭</param>
        /// <param name="fileName">下载文件名；为 null 或空白时不写 Content-Disposition</param>
        /// <param name="status">响应状态码</param>
        public HttpBigDataResult(string filePath, string fileName, HttpStatusCode status = HttpStatusCode.OK)
            : this(File.OpenRead(filePath), HttpMIME.GetType(filePath), status, false, fileName)
        {

        }
    }
}
