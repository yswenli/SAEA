/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Http.Http
*文件名： HttpRequest
*版本号： V3.1.1.0
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
*版本号： V3.1.1.0
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

        RequestDataReader _requestDataReader;

        /// <summary>
        /// init
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="requestDataReader"></param>
        internal void Init(RequestDataReader requestDataReader)
        {
            _requestDataReader = requestDataReader;

            this.Method = _requestDataReader.Method;
            this.Url = _requestDataReader.Url;
            this.RelativeUrl = _requestDataReader.RelativeUrl;
            this.Query = _requestDataReader.Query;
            this.Protocal = _requestDataReader.Protocal;
            this.Headers = _requestDataReader.Headers;
            this.HeaderStr = _requestDataReader.HeaderStr;
            this.Cookies = _requestDataReader.Cookies;
            this.IsFormData = _requestDataReader.IsFormData;
            this.Boundary = _requestDataReader.Boundary;
            this.ContentType = _requestDataReader.ContentType;
            this.ContentLength = _requestDataReader.ContentLength;

            if (_requestDataReader.Forms != null && _requestDataReader.Forms.Count > 0)
            {
                this.Forms = _requestDataReader.Forms;
            }
            if (_requestDataReader.PostFiles != null && _requestDataReader.PostFiles.Count > 0)
            {
                this.PostFiles = _requestDataReader.PostFiles;
            }
            if (!string.IsNullOrEmpty(_requestDataReader.Json))
            {
                this.Json = _requestDataReader.Json;
                this.Body = _requestDataReader.Body;
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
            _requestDataReader.Dispose();
        }
    }
}
