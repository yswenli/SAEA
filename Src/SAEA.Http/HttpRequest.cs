/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Http
*文件名： HttpRequest
*版本号： v5.0.0.1
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
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
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
        public string Json
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

        public string UserHostAddress
        {
            get; private set;
        }

        public string GetRealIp()
        {
            string result = string.Empty;

            if (this.Headers.ContainsKey("http_x_forwarded_for"))
            {
                result = this.Headers["http_x_forwarded_for"];
            }

            if (null == result || result == String.Empty)
            {
                if (this.Headers.ContainsKey("remote_addr"))
                {
                    result = this.Headers["remote_addr"];
                }
            }
            if (null == result || result == String.Empty)
            {
                result = this.UserHostAddress;
            }
            return result;
        }

        public string UserAgent
        {
            get; private set;
        }

        public Browser Browser
        {
            get; private set;
        }

        /// <summary>
        /// contruct
        /// </summary>
        internal HttpRequest()
        {

        }

        HttpMessage _httpMessage;

        /// <summary>
        /// init
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="httpMessage"></param>
        internal void Init(HttpMessage httpMessage)
        {
            _httpMessage = httpMessage;

            this.UserHostAddress = httpMessage.ID.ToIPPort().Item1;
            this.Method = _httpMessage.Method;
            this.Url = _httpMessage.Url;
            this.RelativeUrl = _httpMessage.RelativeUrl;
            this.Query = _httpMessage.Query;
            this.Protocal = _httpMessage.Protocal;
            this.Headers = _httpMessage.Headers;
            this.HeaderStr = _httpMessage.HeaderStr;
            this.Cookies = _httpMessage.Cookies;
            this.IsFormData = _httpMessage.IsFormData;
            this.Boundary = _httpMessage.Boundary;
            this.ContentType = _httpMessage.ContentType;
            this.ContentLength = _httpMessage.ContentLength;

            if (this.Headers == null)
            {
                this.Headers = new Dictionary<string, string>();
            }            

            if (this.Headers.ContainsKey("user-agent"))
            {
                this.UserAgent = this.Headers["user-agent"];

                this.Browser = Browser.Parse(this.UserAgent);
            }
            if (_httpMessage.Forms != null && _httpMessage.Forms.Count > 0)
            {
                this.Forms = _httpMessage.Forms;
            }
            if (_httpMessage.PostFiles != null && _httpMessage.PostFiles.Count > 0)
            {
                this.PostFiles = _httpMessage.PostFiles;
            }
            if (!string.IsNullOrEmpty(_httpMessage.Json))
            {
                this.Json = _httpMessage.Json;
                this.Body = _httpMessage.Body;
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
            _httpMessage.Dispose();
        }
    }
}
