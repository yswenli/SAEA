﻿/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Model
*文件名： ContentResult
*版本号： v7.0.0.1
*唯一标识：4f00dffe-e5f4-4a70-83d3-95c1e16d96a3
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/11 13:51:11
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/11 13:51:11
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using System.Net;
using System.Text;

namespace SAEA.Http.Model
{
    public class HttpContentResult : HttpActionResult
    {
        public HttpContentResult(string str) : this(str, Encoding.UTF8)
        {

        }

        public HttpContentResult(string str, HttpStatusCode status)
        {
            if (!string.IsNullOrEmpty(str))
                this.Content = Encoding.UTF8.GetBytes(str);
            this.ContentEncoding = Encoding.UTF8;
            this.ContentType = "text/html; charset=utf-8";
            this.Status = status;
        }

        public HttpContentResult(string str, Encoding encoding, string contentType = "text/plane; charset=utf-8")
        {
            if (!string.IsNullOrEmpty(str))
                this.Content = encoding.GetBytes(str);
            this.ContentEncoding = encoding;
            this.ContentType = contentType;
        }
    }
}
