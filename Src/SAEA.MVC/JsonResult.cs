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
*文件名： JsonResult
*版本号： v26.4.23.1
*唯一标识：578fe813-f047-4ce2-9ad8-3a4edfd4969c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/29 15:01:17
*描述：JsonResult结果类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:32
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common.Serialization;
using System.Net;
using System.Runtime.Serialization;
using System.Text;

namespace SAEA.MVC
{
    /// <summary>
    /// JSON 响应结果，按指定编码输出 body
    /// </summary>
    [DataContract]
    public class JsonResult : ActionResult
    {
        /// <summary>
        /// 序列化对象并创建 UTF-8 编码的 JSON 结果
        /// </summary>
        /// <param name="model">待序列化对象</param>
        /// <param name="expended">是否输出缩进格式的 JSON</param>
        public JsonResult(object model, bool expended = false) : this(SerializeHelper.Serialize(model, expended))
        {

        }
        /// <summary>
        /// 以 UTF-8 编码创建 JSON 结果
        /// </summary>
        /// <param name="json">JSON 字符串</param>
        public JsonResult(string json) : this(json, Encoding.UTF8)
        {

        }

        /// <summary>
        /// 以 UTF-8 编码和指定状态码创建 JSON 结果
        /// </summary>
        /// <param name="json">JSON 字符串</param>
        /// <param name="status">HTTP 状态码</param>
        public JsonResult(string json, HttpStatusCode status)
        {
            if (!string.IsNullOrEmpty(json))
                this.Content = Encoding.UTF8.GetBytes(json);
            this.ContentEncoding = Encoding.UTF8;
            this.ContentType = "application/json; charset=utf-8";
            this.Status = status;
        }

        /// <summary>
        /// 以指定编码和内容类型创建 JSON 结果
        /// </summary>
        /// <param name="json">JSON 字符串</param>
        /// <param name="encoding">正文编码</param>
        /// <param name="contentType">响应内容类型</param>
        public JsonResult(string json, Encoding encoding, string contentType = "application/json; charset=utf-8")
        {
            if (!string.IsNullOrEmpty(json))
                this.Content = encoding.GetBytes(json);
            this.ContentEncoding = encoding;
            this.ContentType = contentType;
        }
    }
}