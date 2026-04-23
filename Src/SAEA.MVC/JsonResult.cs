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
*唯一标识：37aa9864-49c1-476b-8542-2b119d7b132a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/29 15:01:17
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/10/29 15:01:17
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.Common.Serialization;
using System.Net;
using System.Runtime.Serialization;
using System.Text;

namespace SAEA.MVC
{
    [DataContract]
    public class JsonResult : ActionResult
    {
        public JsonResult(object model, bool expended = false) : this(SerializeHelper.Serialize(model, expended))
        {

        }
        public JsonResult(string json) : this(json, Encoding.UTF8)
        {

        }

        public JsonResult(string json, HttpStatusCode status)
        {
            if (!string.IsNullOrEmpty(json))
                this.Content = Encoding.UTF8.GetBytes(json);
            this.ContentEncoding = Encoding.UTF8;
            this.ContentType = "application/json; charset=utf-8";
            this.Status = status;
        }

        public JsonResult(string json, Encoding encoding, string contentType = "application/json; charset=utf-8")
        {
            if (!string.IsNullOrEmpty(json))
                this.Content = encoding.GetBytes(json);
            this.ContentEncoding = encoding;
            this.ContentType = contentType;
        }
    }
}