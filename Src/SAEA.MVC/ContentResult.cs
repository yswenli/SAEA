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
*修改时间：2018/10/29 15:01:17
*修改人： yswenli
*版本号： v26.4.23.1
*描述：ContentResult结果类
*
*****************************************************************************/
using System.Net;
using System.Runtime.Serialization;
using System.Text;

namespace SAEA.MVC
{
    [DataContract]
    public class ContentResult : ActionResult
    {
        public ContentResult(string str) : this(str, Encoding.UTF8)
        {

        }

        public ContentResult(string str, HttpStatusCode status)
        {
            if (!string.IsNullOrEmpty(str))
                this.Content = Encoding.UTF8.GetBytes(str);
            this.ContentEncoding = Encoding.UTF8;
            this.ContentType = "text/html; charset=utf-8";
            this.Status = status;
        }

        public ContentResult(string str, Encoding encoding, string contentType = "text/plane; charset=utf-8")
        {
            if (!string.IsNullOrEmpty(str))
                this.Content = encoding.GetBytes(str);
            this.ContentEncoding = encoding;
            this.ContentType = contentType;
        }

        public static ContentResult Get(string str)
        {
            return new ContentResult(str);
        }
    }
}