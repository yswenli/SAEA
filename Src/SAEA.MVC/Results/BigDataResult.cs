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
*命名空间：SAEA.MVC.Results
*文件名： BigDataResult
*版本号： v26.4.23.1
*唯一标识：5d662b36-82da-46c3-b5ec-8e195fbc83fe
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/05/20 14:37:39
*描述：BigDataResult接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 10:00:00
*修改人： yswenli
*版本号： v26.11.25.1
*描述：流式正文支持单区间 Range，可满足返回 206、不可满足返回 416
*
*****************************************************************************/
using SAEA.Common.IO;
using SAEA.Http.Base;
using SAEA.Http.Common;
using SAEA.Http.Model;
using SAEA.MVC.Hosting;
using System;
using System.IO;
using System.Net;
using System.Text;

namespace SAEA.MVC.Results
{
    /// <summary>
    /// 大数据结果
    /// </summary>
    public class BigDataResult : ActionResult, IBigDataResult
    {
        /// <summary>
        /// 大数据结果
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="contentType"></param>
        /// <param name="status"></param>
        public BigDataResult(Stream stream, string contentType = "", HttpStatusCode status = HttpStatusCode.OK)
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
            response.Headers["Connection"] = "close";
            response.Status = status;

            long remaining;

            if (parsed.Kind == ByteRangeParseKind.Satisfiable)
            {
                response.Status = HttpStatusCode.PartialContent;
                response.Headers["Accept-Ranges"] = "bytes";
                response.Headers["Content-Range"] = "bytes " + parsed.Start + "-" + parsed.End + "/" + total;
                response.SendHeader(parsed.Length);

                // 报头已写出，后续发生异常不能再改状态码
                (HttpContext.Current as HttpContext)?.MarkResponseStarted();

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

                (HttpContext.Current as HttpContext)?.MarkResponseStarted();

                try
                {
                    response.SendEnd();
                }
                finally
                {
                    stream.Close();
                }

                return;
            }
            else
            {
                response.Headers["Accept-Ranges"] = "bytes";
                response.SendHeader(total);

                // 报头已写出，后续发生异常不能再改状态码
                (HttpContext.Current as HttpContext)?.MarkResponseStarted();

                // 不可定位的流保持原有从当前位置开始读的行为
                if (stream.CanSeek) stream.Position = 0;

                remaining = -1;
            }

            // HEAD 只要报头：Content-Length 已按区间长度或完整长度写出，正文一个字节都不发
            if (response.SuppressBody)
            {
                try
                {
                    response.SendEnd();
                }
                finally
                {
                    // 结束连接失败时同样要释放文件流，否则 HEAD 请求会泄漏句柄
                    stream.Close();
                }

                return;
            }

            var buffer = new byte[10240];
            try
            {
                while (remaining != 0)
                {
                    // 区间请求只读剩余字节数，最后一块按剩余量收窄
                    var readSize = remaining < 0 ? buffer.Length : (int)Math.Min(buffer.Length, remaining);

                    var count = stream.Read(buffer, 0, readSize);

                    if (count < 1)
                    {
                        break;
                    }

                    if (count == buffer.Length)
                    {
                        response.SendData(buffer);
                    }
                    else
                    {
                        var b = new byte[count];

                        Buffer.BlockCopy(buffer, 0, b, 0, count);

                        response.SendData(b);
                    }

                    if (remaining > 0) remaining -= count;
                }

                // 只有正文整体写完才结束连接：中途因客户端断开发送失败时，异常交给响应层按已开始写出的响应收尾
                response.SendEnd();
            }
            finally
            {
                // 无论是否中途失败都要关闭文件流，否则客户端断开一次就泄漏一个句柄
                stream.Close();
            }
        }


        /// <summary>
        /// 大数据结果
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="status"></param>
        public BigDataResult(string filePath, HttpStatusCode status = HttpStatusCode.OK) : this(FileHelper.GetStream(filePath), HttpMIME.GetType(filePath), status)
        {

        }
    }
}
