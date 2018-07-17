/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.WebAPI.Http.Base
*文件名： BaseHeader
*版本号： V1.0.0.0
*唯一标识：2ab45db1-f3f1-4ed5-99c4-5d11f52c8a6b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/8 16:30:45
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/8 16:30:45
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Common;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SAEA.WebAPI.Http.Model
{
    public class HttpBase
    {
        public const string ENTER = "\r\n";

        public const string DENTER = "\r\n\r\n";

        public const string SPACE = " ";

        public string HeaderStr
        {
            get; set;
        }

        public string Method
        {
            get; set;
        }

        public string RelativeUrl
        {
            get; set;
        }

        public string Url
        {
            get; set;
        }
               
        /// <summary>
        /// url参数
        /// </summary>
        public Dictionary<string, string> Query
        {
            get; set;
        }

        /// <summary>
        /// 表单参数
        /// </summary>
        public Dictionary<string, string> Forms
        {
            get; set;
        }
        /// <summary>
        /// request 参数
        /// </summary>
        public Dictionary<string, string> Parmas
        {
            get; set;
        }

        public string Protocal
        {
            get; set;
        }

        public Dictionary<string, string> Cookies
        {
            get; set;
        }


        public Dictionary<string, string> Headers
        {
            get; set;
        }

        /// <summary>
        /// 类型
        /// </summary>
        public string ContentType
        {
            get
            {
                if (this.Headers == null) this.Headers = new Dictionary<string, string>();
                var key = ResponseHeaderType.ContentType.GetDescription();
                if (!this.Headers.ContainsKey(key))
                {
                    return "application/json; charset=utf-8";
                }
                var type = this.Headers[key];

                if (type.IndexOf(";") > 0)
                {
                    type = Regex.Split(type, ";")[0];
                }
                return type;
            }
            set
            {
                if (this.Headers == null) this.Headers = new Dictionary<string, string>();
                var key = ResponseHeaderType.ContentType.GetDescription();
                if (!this.Headers.ContainsKey(key))
                {
                    this.Headers.Add(key, value.ToLower());
                }
                else
                {
                    this.Headers[key] = value.ToLower();
                }
            }
        }


        public bool IsFormData { get; set; } = false;

        public string Boundary
        {
            get; set;
        }

        public int ContentLength
        {
            get; set;
        }

        public byte[] Body
        {
            get; set;
        }

        protected string GetHeader(Enum header)
        {
            if (this.Headers == null) this.Headers = new Dictionary<string, string>();
            var fieldName = header.GetDescription();
            if (fieldName == null) return null;
            var hasKey = Headers.ContainsKey(fieldName);
            if (!hasKey) return null;
            return Headers[fieldName];
        }

        protected void SetHeader(Enum header, string value)
        {
            if (this.Headers == null) this.Headers = new Dictionary<string, string>();
            var fieldName = header.GetDescription();
            if (fieldName == null) return;
            var hasKey = Headers.ContainsKey(fieldName);
            if (hasKey) Headers.Remove(fieldName);
            Headers.Add(fieldName, value);
        }

        protected void RemoveHeader(Enum header, string value)
        {
            if (this.Headers == null) this.Headers = new Dictionary<string, string>();
            var fieldName = header.GetDescription();
            if (fieldName == null) return;
            var hasKey = Headers.ContainsKey(fieldName);
            if (hasKey) Headers.Remove(fieldName);
        }
    }
}
