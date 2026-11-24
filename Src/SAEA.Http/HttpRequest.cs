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
*修改时间：2026/11/24 15:41:00
*修改人： yswenli
*版本号： v26.11.24.1
*描述：请求体改为只读 Stream，初始化时借用底层消息的流，释放与复位只置空
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http.Base;
using SAEA.Http.Model;

using System;
using System.Collections.Generic;
using System.IO;

namespace SAEA.Http
{
    /// <summary>
    /// HTTP请求定义
    /// </summary>
    public class HttpRequest : HttpBase, IDisposable
    {
        // 显式写入的值优先于底层消息，保持「写入什么就读到什么」；
        // 未写入时由底层消息按需解码，报头缓冲归还后读取结果为 null
        string _headerStr;

        /// <summary>
        /// 原始报头文本，未显式写入时按需从底层消息解码；底层消息释放后为 null
        /// </summary>
        public override string HeaderStr
        {
            get
            {
                var value = _headerStr;

                if (value != null) return value;

                var httpMessage = _httpMessage;

                return httpMessage == null ? null : httpMessage.HeaderStr;
            }
            set
            {
                _headerStr = value;
            }
        }

        /// <summary>
        /// 请求携带的 Cookie 集合，未绑定底层消息时退回本实例的空集合
        /// <para>
        /// 底层消息在首次读取时才解析 Cookie 报头，未读 Cookie 的请求不产生解析分配；
        /// 消息释放后本属性退回空集合，与新建实例的初始状态一致。
        /// </para>
        /// </summary>
        public override HttpCookies Cookies
        {
            get
            {
                var httpMessage = _httpMessage;

                return httpMessage == null ? base.Cookies : httpMessage.Cookies;
            }
            set
            {
                base.Cookies = value;
            }
        }

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
        /// 请求体流，只读且可 Seek；无请求体时为 null，生命周期与请求一致
        /// </summary>
        public Stream Body
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

            // Cookie 不在这里取：底层消息按需解析，此处读取会让每个请求都付出一次解析
            this.IsFormData = _httpMessage.IsFormData;
            this.Boundary = _httpMessage.Boundary;
            this.ContentType = _httpMessage.ContentType;
            this.ContentLength = _httpMessage.ContentLength;

            // 请求体流归底层消息持有：这里只借用引用，释放由 HttpMessage 统一负责
            this.Body = _httpMessage.Body;

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
            }
        }

        /// <summary>
        /// 按名读取请求携带的 Cookie，不触发整段 Cookie 解析
        /// </summary>
        /// <param name="name">Cookie 名，区分大小写，与集合键的规则一致</param>
        /// <param name="value">Cookie 值；名字不存在时为 null</param>
        /// <returns>存在该名字的 Cookie 返回 true</returns>
        internal bool TryGetCookieValue(string name, out string value)
        {
            var httpMessage = _httpMessage;

            if (httpMessage == null)
            {
                value = null;

                return false;
            }

            return httpMessage.TryGetCookieValue(name, out value);
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
        /// 复位请求，使同一个实例可以承载下一条请求；复位后的状态与新建实例一致。
        /// </summary>
        internal void ResetForNextRequest()
        {
            _httpMessage = null;

            this.UserHostAddress = null;
            this.Method = null;
            this.Url = null;
            this.RelativeUrl = null;
            this.Protocal = null;
            this.HeaderStr = null;
            this.IsFormData = false;
            this.Boundary = null;
            this.ContentLength = 0;
            this.Json = null;
            this.PostFiles = null;
            this.Body = null;

            // 报头、查询与 Cookie 集合由底层消息持有，解码器已在下一次解析前清空；
            // 这里再清一次会把刚解析好的本次请求内容抹掉，只清请求对象独占的参数集合
            if (this.Parmas != null) this.Parmas.Clear();
        }

        /// <summary>
        /// 释放底层消息资源；可重复调用，未初始化或已释放时直接返回
        /// </summary>
        public void Dispose()
        {
            var httpMessage = _httpMessage;

            if (httpMessage == null) return;

            _httpMessage = null;

            httpMessage.Dispose();

            // 底层消息已在上一步处置正文流与文件分段，这里只摘掉引用，避免空闲连接继续持有
            this.Json = null;
            this.PostFiles = null;
            this.Body = null;

            if (this.Parmas != null) this.Parmas.Clear();
        }
    }
}