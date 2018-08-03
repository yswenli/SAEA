/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.WebAPI.Http
*文件名： HttpRequest
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Sockets.Interface;
using SAEA.WebAPI.Http.Base;
using SAEA.WebAPI.Http.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SAEA.WebAPI.Http
{
    /// <summary>
    /// HTTP请求定义
    /// </summary>
    public class HttpRequest : HttpBase, IDisposable
    {
        internal HttpServer HttpServer { get; set; }

        internal IUserToken UserToken { get; set; }

        RequestDataReader _requestDataReader;


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
        /// <param name="httpServer"></param>
        /// <param name="userToken"></param>
        /// <param name="requestDataReader"></param>
        internal void Init(HttpServer httpServer, IUserToken userToken, RequestDataReader requestDataReader)
        {
            this.HttpServer = httpServer;
            this.UserToken = userToken;

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
            _requestDataReader = requestDataReader;
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
            _requestDataReader.Dispose();
        }
    }
}
