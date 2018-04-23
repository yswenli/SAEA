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
using SAEA.Commom;
using SAEA.Sockets.Interface;
using SAEA.WebAPI.Http.Base;
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
    public class HttpRequest : BaseHeader
    {
        /// <summary>
        /// URL参数
        /// </summary>
        public NameValueCollection Params { get; private set; } = new NameValueCollection();

        /// <summary>
        /// HTTP请求方式
        /// </summary>
        public string Method { get; private set; }

        /// <summary>
        /// HTTP(S)地址
        /// </summary>
        public string URL { get; set; }

        public string Query { get; set; }

        /// <summary>
        /// 定义缓冲区
        /// </summary>
        private const int MAX_SIZE =10* 1024;

        private byte[] bytes = new byte[MAX_SIZE];

        private Stream _dataStream;


        internal HttpServer HttpServer { get; set; }

        internal IUserToken UserToken { get; set; }

        /// <summary>
        /// 初始化
        /// </summary>
        internal HttpRequest()
        {

        }

        internal void Init(HttpServer httpServer, IUserToken userToken, Stream stream)
        {
            this.HttpServer = httpServer;
            this.UserToken = userToken;

            this._dataStream = stream;

            var data = GetRequestData(_dataStream);
            var rows = Regex.Split(data, Environment.NewLine);

            //Request URL & Method & Version
            var first = Regex.Split(rows[0], @"(\s+)")
                .Where(e => e.Trim() != string.Empty)
                .ToArray();
            if (first.Length > 0) this.Method = first[0];
            if (first.Length > 1)
            {
                this.Query = first[1];

                if (this.Query.Contains("?"))
                {
                    var qarr = this.Query.Split("?");
                    this.URL = qarr[0];
                    this.Params = GetRequestParameters(qarr[1]);
                }
                else
                {
                    this.URL = this.Query;
                }

                var uarr = this.URL.Split("/");

                if (long.TryParse(uarr[uarr.Length - 1], out long id))
                {
                    this.URL = this.URL.Substring(0, this.URL.LastIndexOf("/"));
                    this.Params.Set("id", id.ToString());
                }
            }
            if (first.Length > 2) this.Protocols = first[2];

            //Request Headers
            this.Headers = GetRequestHeaders(rows);

            //Request "GET"
            if (this.Method == "GET")
            {
                this.Body = GetRequestBody(rows);
            }

            //Request "POST"
            if (this.Method == "POST")
            {
                this.Body = GetRequestBody(rows);
                var contentType = GetHeader(RequestHeaderType.ContentType);
                var isUrlencoded = contentType == @"application/x-www-form-urlencoded";
                if (isUrlencoded) this.Params = GetRequestParameters(this.Body);
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

        private string GetRequestData(Stream stream)
        {
            var length = 0;
            var data = string.Empty;

            do
            {
                length = stream.Read(bytes, 0, MAX_SIZE - 1);
                data += Encoding.UTF8.GetString(bytes, 0, length);
            } while (length > 0 && !data.Contains(DENTER));

            return data;
        }

        private string GetRequestBody(IEnumerable<string> rows)
        {
            var target = rows.Select((v, i) => new { Value = v, Index = i }).FirstOrDefault(e => e.Value.Trim() == string.Empty);
            if (target == null) return null;
            var range = Enumerable.Range(target.Index + 1, rows.Count() - target.Index - 1);
            return string.Join(Environment.NewLine, range.Select(e => rows.ElementAt(e)).ToArray());
        }

        private NameValueCollection GetRequestHeaders(IEnumerable<string> rows)
        {
            if (rows == null || rows.Count() <= 0) return null;
            var target = rows.Select((v, i) => new { Value = v, Index = i }).FirstOrDefault(e => e.Value.Trim() == string.Empty);
            var length = target == null ? rows.Count() - 1 : target.Index;
            if (length <= 1) return null;
            var range = Enumerable.Range(1, length - 1);
            return range.Select(e => rows.ElementAt(e)).Distinct().ToDictionary(e => e.Split(':')[0], e => e.Split(':')[1].Trim()).ToNameValueCollection();
        }

        private NameValueCollection GetRequestParameters(string row)
        {
            if (string.IsNullOrEmpty(row)) return null;
            var kvs = Regex.Split(row, "&");
            if (kvs == null || kvs.Count() <= 0) return null;

            return kvs.ToDictionary(e => Regex.Split(e, "=")[0], e => Regex.Split(e, "=")[1]).ToNameValueCollection();
        }

        internal void Clear()
        {
            this.Params.Clear();
            this.Query = this.URL = string.Empty;
            this.Headers.Clear();
            if (this._dataStream != null)
            {
                this._dataStream.Close();
            }
            this._dataStream = null;
        }
    }
}
