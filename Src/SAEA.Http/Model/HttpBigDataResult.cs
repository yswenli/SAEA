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
*修改时间：2020/05/20 14:37:39
*修改人： yswenli
*版本号： v26.4.23.1
*描述：HttpBigDataResult接口
*
*****************************************************************************/
using SAEA.Http.Base;
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

            HttpContext.Current.Response.ContentType = contentType;
            HttpContext.Current.Response.Status = status;
            HttpContext.Current.Response.SendHeader(total);

            const int bufferSize = 1024;
            byte[] buffer = null;

            try
            {
                buffer = MemoryPoolManager.Rent(bufferSize);

                do
                {
                    var count = stream.Read(buffer, 0, bufferSize);

                    if (count < 1)
                    {
                        break;
                    }

                    if (count == bufferSize)
                    {
                        HttpContext.Current.Response.SendData(buffer.AsSpan(0, bufferSize).ToArray());
                    }
                    else
                    {
                        HttpContext.Current.Response.SendData(buffer.AsSpan(0, count).ToArray());
                    }

                }
                while (true);
            }
            finally
            {
                if (buffer != null)
                {
                    MemoryPoolManager.Return(buffer, bufferSize);
                }
            }

            HttpContext.Current.Response.SendEnd();
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
