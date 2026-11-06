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
*修改时间：2026/11/6 13:23:31
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补齐英文与乱码注释的中文说明
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
        /// 报文体按 application/json 解析出的原始文本
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

        /// <summary>
        /// 当前连接的客户端标识，Init 时取自 HttpMessage.ID
        /// </summary>
        public string UserHostAddress
        {
            get; private set;
        }

        /// <summary>
        /// 获取客户端真实 IP，依次取代理转发头、远端地址与连接标识
        /// </summary>
        /// <returns>IP 地址文本或连接标识</returns>
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

        /// <summary>
        /// 客户端 User-Agent，缺失时返回空字符串
        /// </summary>
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


        /// <summary>
        /// 由 User-Agent 解析出的浏览器信息，User-Agent 为空时为 null
        /// </summary>
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
        /// 创建空实例，供连接复用时重置状态
        /// </summary>
        internal HttpRequest()
        {

        }

        HttpMessage _httpMessage;

        /// <summary>
        /// 用底层 HTTP 消息初始化请求对象
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="httpMessage">解析后的 HTTP 消息</param>
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

        /// <summary>
        /// 读取请求报头
        /// </summary>
        /// <param name="header">请求报头枚举</param>
        /// <returns>报头值，键不存在时抛出 KeyNotFoundException</returns>
        public string GetHeader(RequestHeaderType header)
        {
            return base.GetHeader(header);
        }

        /// <summary>
        /// 写入请求报头
        /// </summary>
        /// <param name="header">请求报头枚举</param>
        /// <param name="value">报头值</param>
        public void SetHeader(RequestHeaderType header, string value)
        {
            base.SetHeader(header, value);
        }

        /// <summary>
        /// 释放底层消息资源，未初始化时抛出 NullReferenceException
        /// </summary>
        public void Dispose()
        {
            _httpMessage.Dispose();
        }
    }
}