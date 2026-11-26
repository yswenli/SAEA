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
*修改时间：2026/11/26 10:00:00
*修改人： yswenli
*版本号： v26.11.25.1
*描述：流式正文支持单区间 Range，可满足返回 206、不可满足返回 416
*
*****************************************************************************/
using SAEA.Http.Base;
using SAEA.Http.Common;
using SAEA.Common.Caching;
using System;
using System.IO;
using System.Net;
using System.Text;

namespace SAEA.Http.Model
{
    /// <summary>
    /// 大数据结果
    /// </summary>
    public class HttpBigDataResult : HttpActionResult, IBigDataResult
    {
        /// <summary>
        /// 大数据结果
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="contentType"></param>
        /// <param name="status"></param>
        public HttpBigDataResult(Stream stream, string contentType = "", HttpStatusCode status = HttpStatusCode.OK)
        {
            var total = stream.Length;

            this.ContentEncoding = Encoding.UTF8;
            this.ContentType = contentType;
            this.Status = status;

            var response = HttpContext.Current.Response;

            string rangeHeader;

            HttpContext.Current.Request.Headers.TryGetValue("Range", out rangeHeader);

            // 只有可定位且长度已知的流才能按区间定位；其余按完整响应处理
            var parsed = stream.CanSeek && total >= 0
                ? ByteRangeHeader.Parse(rangeHeader, total)
                : default(ByteRangeParseResult);

            response.ContentType = contentType;
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
            else
            {
                response.Headers["Accept-Ranges"] = "bytes";
                response.SendHeader(total);

                // 不可定位的流保持原有从当前位置开始读的行为
                if (stream.CanSeek) stream.Position = 0;

                remaining = -1;
            }

            const int bufferSize = 1024;
            byte[] buffer = null;

            try
            {
                buffer = MemoryPoolManager.Rent(bufferSize);

                while (remaining != 0)
                {
                    // 区间请求只读剩余字节数，最后一块按剩余量收窄
                    var readSize = remaining < 0 ? bufferSize : (int)Math.Min(bufferSize, remaining);

                    var count = stream.Read(buffer, 0, readSize);

                    if (count < 1)
                    {
                        break;
                    }

                    if (count == bufferSize)
                    {
                        response.SendData(buffer.AsSpan(0, bufferSize).ToArray());
                    }
                    else
                    {
                        response.SendData(buffer.AsSpan(0, count).ToArray());
                    }

                    if (remaining > 0) remaining -= count;
                }
            }
            finally
            {
                if (buffer != null)
                {
                    MemoryPoolManager.Return(buffer, bufferSize);
                }
            }

            response.SendEnd();
        }


        /// <summary>
        /// 大数据结果
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="status"></param>
        public HttpBigDataResult(string filePath, HttpStatusCode status = HttpStatusCode.OK) : this(File.OpenRead(filePath), HttpMIME.GetType(filePath), status)
        {

        }
    }
}
