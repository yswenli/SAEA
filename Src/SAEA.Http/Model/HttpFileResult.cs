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
*文件名： HttpFileResult
*版本号： v26.4.23.1
*唯一标识：0140276c-adea-45a0-85a5-866e29623c81
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/29 15:01:17
*描述：HttpFileResult接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 17:46:21
*修改人： yswenli
*版本号： v26.11.26.1
*描述：实现 IAttachmentResult 并新增带 fileName 的构造重载
*
*****************************************************************************/
using SAEA.Common.IO;
using SAEA.Http.Base;
using SAEA.Http.Common;
using System.IO;
using System.Net;
using System.Text;

namespace SAEA.Http.Model
{
    /// <summary>
    /// 小文件或数据
    /// </summary>
    public class HttpFileResult : HttpActionResult, IFileResult, IAttachmentResult
    {
        /// <summary>
        /// 文件内容字节，隐藏基类同名属性
        /// </summary>
        public new byte[] Content { get; set; }

        /// <summary>
        /// 下载文件名；为 null 或空白表示不写 Content-Disposition
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// 小文件或数据
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="isStaticsCached"></param>
        public HttpFileResult(string filePath, bool isStaticsCached) : this(isStaticsCached, filePath, HttpMIME.GetType(filePath), HttpStatusCode.OK, null)
        {

        }

        /// <summary>
        /// 小文件或数据，并指定下载文件名
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="isStaticsCached">是否使用静态资源缓存</param>
        /// <param name="fileName">下载文件名；为 null 或空白时不写 Content-Disposition</param>
        public HttpFileResult(string filePath, bool isStaticsCached, string fileName) : this(isStaticsCached, filePath, HttpMIME.GetType(filePath), HttpStatusCode.OK, fileName)
        {

        }

        /// <summary>
        /// 小文件或数据
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="contentType"></param>
        /// <param name="status"></param>
        public HttpFileResult(Stream stream, string contentType = "", HttpStatusCode status = HttpStatusCode.OK) : this(stream, contentType, status, null)
        {

        }

        /// <summary>
        /// 小文件或数据，并指定下载文件名
        /// </summary>
        /// <param name="stream">正文数据流，读取后由调用方负责释放</param>
        /// <param name="contentType">响应内容类型</param>
        /// <param name="status">响应状态码</param>
        /// <param name="fileName">下载文件名；为 null 或空白时不写 Content-Disposition</param>
        public HttpFileResult(Stream stream, string contentType, HttpStatusCode status, string fileName)
        {
            int len = (int)stream.Length;
            var data = new byte[len];
            stream.Read(data, 0, len);
            this.Content = data;
            this.ContentEncoding = Encoding.UTF8;
            this.ContentType = contentType;
            this.Status = status;
            this.FileName = fileName;
        }


        /// <summary>
        /// 文件内容
        /// </summary>
        /// <param name="isStaticsCached"></param>
        /// <param name="filePath"></param>
        /// <param name="contentType"></param>
        /// <param name="status"></param>
        public HttpFileResult(bool isStaticsCached, string filePath, string contentType = "", HttpStatusCode status = HttpStatusCode.OK) : this(isStaticsCached, filePath, contentType, status, null)
        {

        }

        /// <summary>
        /// 文件内容，并指定下载文件名
        /// </summary>
        /// <param name="isStaticsCached">是否使用静态资源缓存</param>
        /// <param name="filePath">文件路径</param>
        /// <param name="contentType">响应内容类型</param>
        /// <param name="status">响应状态码</param>
        /// <param name="fileName">下载文件名；为 null 或空白时不写 Content-Disposition</param>
        public HttpFileResult(bool isStaticsCached, string filePath, string contentType, HttpStatusCode status, string fileName)
        {
            if (isStaticsCached)
            {
                this.Content = StaticResourcesCache.GetOrAdd(filePath, filePath);
            }
            else
            {
                this.Content = FileHelper.Read(filePath);
            }
            this.ContentEncoding = Encoding.UTF8;
            this.ContentType = contentType;
            this.Status = status;
            this.FileName = fileName;
        }
    }
}