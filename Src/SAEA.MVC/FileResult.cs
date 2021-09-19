/****************************************************************************
*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC
*文件名： FileResult
*版本号： v6.0.0.1
*唯一标识：10362e3c-aeb1-4282-a3b2-69774773af9e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/16 13:16:32
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/16 13:16:32
*修改人： yswenli
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/
using System.IO;
using System.Net;
using System.Text;

using SAEA.Common.IO;
using SAEA.Http.Base;
using SAEA.Http.Common;
using SAEA.Http.Model;

namespace SAEA.MVC
{
    /// <summary>
    /// 小文件或数据
    /// </summary>
    public class FileResult : ActionResult, IFileResult
    {
        public new byte[] Content { get; set; }

        /// <summary>
        /// 小文件或数据
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="isStaticsCached"></param>
        public FileResult(string filePath, bool isStaticsCached) : this(isStaticsCached, filePath, HttpMIME.GetType(filePath))
        {

        }

        /// <summary>
        /// 小文件或数据
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="contentType"></param>
        /// <param name="status"></param>
        public FileResult(Stream stream, string contentType = "", HttpStatusCode status = HttpStatusCode.OK)
        {
            int len = (int)stream.Length;
            var data = new byte[len];
            stream.Position = 0;
            stream.Read(data, 0, len);
            Content = data;
            ContentEncoding = Encoding.UTF8;
            ContentType = contentType;
            Status = status;
            stream.Close();
        }

        /// <summary>
        /// 文件内容
        /// </summary>
        /// <param name="isStaticsCached"></param>
        /// <param name="filePath"></param>
        /// <param name="contentType"></param>
        /// <param name="status"></param>
        public FileResult(bool isStaticsCached, string filePath, string contentType = "", HttpStatusCode status = HttpStatusCode.OK)
        {
            this.ContentEncoding = Encoding.UTF8;
            this.ContentType = contentType;
            this.Status = status;

            if (isStaticsCached)
            {
                this.Content = StaticResourcesCache.GetOrAdd(filePath, filePath);
            }
            else
            {
                this.Content = FileHelper.Read(filePath);
            }

        }
    }
}
