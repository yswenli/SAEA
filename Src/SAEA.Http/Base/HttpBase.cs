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
*命名空间：SAEA.Http.Base
*文件名： HttpBase
*版本号： v26.4.23.1
*唯一标识：9a706742-da4d-41ff-8e41-137dcb5c269d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：HttpBase类
*
*=====================================================================
*修改标记
*修改时间：2018/10/28 14:19:50
*修改人： yswenli
*版本号： v26.4.23.1
*描述：HttpBase类
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
                if (string.IsNullOrEmpty(type))
                {
                    if (Method.Equals("GET", StringComparison.InvariantCultureIgnoreCase))
                    {
                        type = "text/html";
                    }
                    else
                    {
                        type = "application/json; charset=UTF-8";
                    }
                    Headers[key] = type;
                }
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