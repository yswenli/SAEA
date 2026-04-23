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
*命名空间：SAEA.Http
*文件名： HttpRequest
*版本号： v26.4.23.1
*唯一标识：534c3548-e81d-4251-8aa9-0cbf8f86fe80
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：HttpRequest类
*
*=====================================================================
*修改标记
*修改时间：2018/10/28 14:19:50
*修改人： yswenli
*版本号： v26.4.23.1
*描述：HttpRequest类
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
        /// Json
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
            else if (this.Headers.ContainsKey("x_forwarded_for"))
            {
                result = this.Headers["x_forwarded_for"];
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
            get
            {
                if (this.Headers.ContainsKey("user-agent"))
                {
                    return this.Headers["user-agent"];
                }
                return string.Empty;
            }
        }


        public Browser Browser
        {
            get
            {
                if (!string.IsNullOrEmpty(UserAgent))
                {
                    return Browser.Parse(UserAgent);
                }
                return null;
            }
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

            this.UserHostAddress = httpMessage.ID;
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