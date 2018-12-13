/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Http
*文件名： HttpCookie
*版本号： V3.3.3.5
*唯一标识：2e43075f-a43d-4b60-bee1-1f9107e2d133
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/12/12 20:46:40
*描述：
*
*=====================================================================
*修改标记
*创建时间：2018/12/12 20:46:40
*修改人： yswenli
*版本号： V3.3.3.5
*描述：
******************************************************************************/
using SAEA.Common;
using SAEA.Http.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.Http
{
    public class HttpCookie
    {
        public string Key
        {
            get; set;
        }

        public string Value
        {
            get; set;
        }

        public DateTime Expires
        {
            get; set;
        }

        public string Path
        {
            get; set;
        }

        public string Domain
        {
            get; set;
        }


        public HttpCookie(string key, string value) : this(key, value, DateTime.Now)
        {

        }

        public HttpCookie(string key, string value, string domain) : this(key, value, DateTime.Now.AddMinutes(20), "/", string.Empty)
        {

        }

        public HttpCookie(string key, string value, DateTime expires) : this(key, value, expires, "/", string.Empty)
        {

        }

        public HttpCookie(string key, string value, DateTime expires, string path, string domain)
        {
            this.Key = key;
            this.Value = value;
            this.Expires = expires;
            this.Path = path;
            this.Domain = domain;
        }


        public new string ToString()
        {
            if (string.IsNullOrEmpty(this.Domain))
            {
                return $"Set-Cookie: {Key}={Value}; Expires={Expires.ToFString("r")}; Path={Path}";
            }
            return $"Set-Cookie: {Key}={Value}; Expires={Expires.ToFString("r")}; Path={Path}; Domain={Domain}";
        }
    }


    public class HttpCookies : Dictionary<string, HttpCookie>
    {
        public static HttpCookies Parse(string cookieStr)
        {
            var result = new HttpCookies();

            if (string.IsNullOrEmpty(cookieStr)) return result;

            var rows = cookieStr.Split(";", StringSplitOptions.RemoveEmptyEntries);

            if (rows == null || rows.Count() <= 0) return result;

            foreach (var row in rows)
            {
                var rowArr = row.Split("=");
                result[rowArr[0]] = new HttpCookie(rowArr[0], HttpUtility.HtmlDecode(HttpUtility.UrlDecode(rowArr[1])));
            }
            return result;
        }

        public HttpCookies() : base()
        {

        }

        public new string ToString()
        {
            if (this.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var item in this.Values)
                {
                    sb.AppendLine(item.ToString());
                }
                return sb.ToString();
            }
            return string.Empty;
        }
    }
}
