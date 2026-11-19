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
*修改时间：2026/11/19 16:54:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：写正文循环加 try/finally，客户端中途断开时也关闭文件流；SendEnd 只在正文写完时调用
*
*****************************************************************************/
using SAEA.Common.IO;
using SAEA.Http.Base;
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

            HttpContext.Current.Response.ContentType = contentType;
            HttpContext.Current.Response.Headers["Connection"] = "close";
            HttpContext.Current.Response.Status = status;
            HttpContext.Current.Response.SendHeader(total);

            // 报头已写出，后续发生异常不能再改状态码
            (HttpContext.Current as HttpContext)?.MarkResponseStarted();

            // HEAD 只要报头：Content-Length 已按完整长度写出，正文一个字节都不发
            if (HttpContext.Current.Response.SuppressBody)
            {
                try
                {
                    HttpContext.Current.Response.SendEnd();
                }
                finally
                {
                    // 结束连接失败时同样要释放文件流，否则 HEAD 请求会泄漏句柄
                    stream.Close();
                }

                return;
            }

            var buffer = new byte[10240];
            stream.Position = 0;
            try
            {
                do
                {
                    var count = stream.Read(buffer, 0, buffer.Length);

                    if (count < 1)
                    {
                        break;
                    }

                    if (count == 10240)
                    {
                        HttpContext.Current.Response.SendData(buffer);
                    }
                    else
                    {
                        var b = new byte[count];

                        Buffer.BlockCopy(buffer, 0, b, 0, count);

                        HttpContext.Current.Response.SendData(b);
                    }

                }
                while (true);

                // 只有正文整体写完才结束连接：中途因客户端断开发送失败时，异常交给响应层按已开始写出的响应收尾
                HttpContext.Current.Response.SendEnd();
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
