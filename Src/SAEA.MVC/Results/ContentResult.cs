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
*命名空间：SAEA.MVC
*文件名： ContentResult
*版本号： v26.4.23.1
*唯一标识：e6eb47cb-e376-4da2-81a2-6410308969d2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/29 15:01:17
*描述：ContentResult结果类
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 17:43:19
*修改人： yswenli
*版本号： v26.11.16.1
*描述：构造参数默认内容类型改为 text/plain
*
*****************************************************************************/
using System.Net;
using System.Runtime.Serialization;
using System.Text;

namespace SAEA.MVC
{
    /// <summary>
    /// 文本内容响应结果，按指定编码输出 body
    /// </summary>
    [DataContract]
    public class ContentResult : ActionResult
    {
        /// <summary>
        /// 以 UTF-8 编码创建文本内容结果
        /// </summary>
        /// <param name="str">响应正文</param>
        public ContentResult(string str) : this(str, Encoding.UTF8)
        {

        }

        /// <summary>
        /// 以 UTF-8 编码和指定状态码创建 text/html 文本内容结果
        /// </summary>
        /// <param name="str">响应正文</param>
        /// <param name="status">HTTP 状态码</param>
        public ContentResult(string str, HttpStatusCode status)
        {
            if (!string.IsNullOrEmpty(str))
                this.Content = Encoding.UTF8.GetBytes(str);
            this.ContentEncoding = Encoding.UTF8;
            this.ContentType = "text/html; charset=utf-8";
            this.Status = status;
        }

        /// <summary>
        /// 以指定编码和内容类型创建文本内容结果
        /// </summary>
        /// <param name="str">响应正文</param>
        /// <param name="encoding">正文编码</param>
        /// <param name="contentType">响应内容类型</param>
        public ContentResult(string str, Encoding encoding, string contentType = "text/plain; charset=utf-8")
        {
            if (!string.IsNullOrEmpty(str))
                this.Content = encoding.GetBytes(str);
            this.ContentEncoding = encoding;
            this.ContentType = contentType;
        }

        /// <summary>
        /// 创建 UTF-8 编码的文本内容结果
        /// </summary>
        /// <param name="str">响应正文</param>
        /// <returns>文本内容结果</returns>
        public static ContentResult Get(string str)
        {
            return new ContentResult(str);
        }
    }
}