/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Http.Http
*文件名： HttpRequest
*版本号： V3.0.0.1
*唯一标识：eeefb8e0-9493-4a07-b469-fc24db360a1b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/8 16:34:03
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/8 16:34:03
*修改人： yswenli
*版本号： V3.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Http.Base;
using SAEA.Http.Model;
using System;
using System.Collections.Generic;

namespace SAEA.Http
{
    /// <summary>
    /// HTTP请求定义
    /// </summary>
    public class HttpRequest : HttpBase, IDisposable
    {
        /// <summary>
        /// enctype="text/plain"
        /// </summary>
        internal string Json
        {
            get; set;
        }

        /// <summary>
        /// 接收到的文件信息
        /// </summary>
        public List<FilePart> PostFiles
        {
            get; set;
        }

        /// <summary>
        /// contruct
        /// </summary>
        internal HttpRequest()
        {

        }

        /// <summary>
        /// init
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="requestDataReader"></param>
        internal void Init(RequestDataReader requestDataReader)
        {
            this.Method = requestDataReader.Method;
            this.Url = requestDataReader.Url;
            this.RelativeUrl = requestDataReader.RelativeUrl;
            this.Query = requestDataReader.Query;
            this.Protocal = requestDataReader.Protocal;
            this.Headers = requestDataReader.Headers;
            this.HeaderStr = requestDataReader.HeaderStr;
            this.Cookies = requestDataReader.Cookies;
            this.IsFormData = requestDataReader.IsFormData;
            this.Boundary = requestDataReader.Boundary;
            this.ContentType = requestDataReader.ContentType;
            this.ContentLength = requestDataReader.ContentLength;

            if (requestDataReader.Forms != null && requestDataReader.Forms.Count > 0)
            {
                this.Forms = requestDataReader.Forms;
            }
            if (requestDataReader.PostFiles != null && requestDataReader.PostFiles.Count > 0)
            {
                this.PostFiles = requestDataReader.PostFiles;
            }
            if (!string.IsNullOrEmpty(requestDataReader.Json))
            {
                this.Json = requestDataReader.Json;
                this.Body = requestDataReader.Body;
            }
        }

        public string GetHeader(RequestHeaderType header)
        {
            return base.GetHeader(header);
        }

        public void SetHeader(RequestHeaderType header, string value)
        {
            base.SetHeader(header, value);
        }

        public void Dispose()
        {
            if (this.Query != null)
                this.Query.Clear();
            if (this.Forms != null)
                this.Forms.Clear();
            if (this.Parmas != null)
                this.Parmas.Clear();
            this.RelativeUrl = this.Url = string.Empty;
            this.Headers.Clear();
        }
    }
}
