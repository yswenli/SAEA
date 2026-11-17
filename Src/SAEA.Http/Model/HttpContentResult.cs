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
*文件名： HttpContentResult
*版本号： v26.4.23.1
*唯一标识：1d2a2c61-ee09-4f24-9699-86cab8382e54
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/29 15:01:17
*描述：HttpContentResult结果类
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 18:40:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：修正默认内容类型的 text/plane 笔误为 text/plain
*
*****************************************************************************/
using System.Net;
using System.Text;

namespace SAEA.Http.Model
{
    /// <summary>
    /// 文本内容结果，按指定编码把字符串输出为报文体
    /// </summary>
    public class HttpContentResult : HttpActionResult
    {
        /// <summary>
        /// 以 UTF-8 输出文本
        /// </summary>
        /// <param name="str">响应文本</param>
        public HttpContentResult(string str) : this(str, Encoding.UTF8)
        {

        }

        /// <summary>
        /// 以 UTF-8 输出文本并指定状态码，内容类型固定为 text/html
        /// </summary>
        /// <param name="str">响应文本</param>
        /// <param name="status">响应状态码</param>
        public HttpContentResult(string str, HttpStatusCode status)
        {
            if (!string.IsNullOrEmpty(str))
                this.Content = Encoding.UTF8.GetBytes(str);
            this.ContentEncoding = Encoding.UTF8;
            this.ContentType = "text/html; charset=utf-8";
            this.Status = status;
        }

        /// <summary>
        /// 按指定编码与内容类型输出文本，状态码保持 200
        /// </summary>
        /// <param name="str">响应文本</param>
        /// <param name="encoding">文本编码</param>
        /// <param name="contentType">内容类型，默认 text/plain; charset=utf-8</param>
        public HttpContentResult(string str, Encoding encoding, string contentType = "text/plain; charset=utf-8")
        {
            if (!string.IsNullOrEmpty(str))
                this.Content = encoding.GetBytes(str);
            this.ContentEncoding = encoding;
            this.ContentType = contentType;
        }
    }
}