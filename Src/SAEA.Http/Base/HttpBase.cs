/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Base
*文件名： HttpBase
*版本号： v7.0.0.1
*唯一标识：a303db7d-f83c-4c49-9804-032ec2236232
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/10 13:58:08
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/10 13:58:08
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;

using SAEA.Common;
using SAEA.Http.Model;

namespace SAEA.Http.Base
{
    public class HttpBase
    {
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
        } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 表单参数
        /// </summary>
        public Dictionary<string, string> Forms
        {
            get; set;
        } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// request 参数
        /// </summary>
        public Dictionary<string, string> Parmas
        {
            get; set;
        } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Protocal
        {
            get; set;
        }

        public HttpCookies Cookies
        {
            get; set;
        } = new HttpCookies();


        public Dictionary<string, string> Headers
        {
            get; set;
        } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 类型
        /// </summary>
        public string ContentType
        {
            get
            {
                var key = ResponseHeaderType.ContentType.GetDescription();

                Headers.TryGetValue(key, out string type);
                
                return type;
            }
            set
            {
                var key = ResponseHeaderType.ContentType.GetDescription();
                if (!this.Headers.ContainsKey(key))
                {
                    this.Headers.Add(key, value);
                }
                else
                {
                    this.Headers[key] = value;
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
            var fieldName = header.GetDescription();
            if (fieldName == null) return null;
            return Headers[fieldName];
        }

        protected void SetHeader(Enum header, string value)
        {
            var fieldName = header.GetDescription();
            if (fieldName == null) return;
            Headers[fieldName] = value;
        }

        protected void RemoveHeader(Enum header, string value)
        {
            var fieldName = header.GetDescription();
            if (fieldName == null) return;
            var hasKey = Headers.ContainsKey(fieldName);
            if (hasKey) Headers.Remove(fieldName);
        }
    }
}
