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
*修改时间：2026/11/26 10:00:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：Cookies 改为虚成员，供底层消息实现按需解析 Cookie 报头
*
*****************************************************************************/
using System;
using System.Collections.Generic;

using SAEA.Common;
using SAEA.Http.Model;

namespace SAEA.Http.Base
{
    /// <summary>
    /// Http 请求与响应的公共基类，承载方法、地址、报头、Cookie 与报文体
    /// </summary>
    public class HttpBase
    {
        // 键名在读取 ContentType 时每请求都要用，缓存一次即可，避免枚举装箱与并发字典查找
        static readonly string _contentTypeKey = ResponseHeaderType.ContentType.GetDescription();

        string _headerStr;

        /// <summary>
        /// 原始报头文本；派生类可覆盖为按需生成，读取结果与写入值保持一致
        /// </summary>
        public virtual string HeaderStr
        {
            get { return _headerStr; }
            set { _headerStr = value; }
        }

        /// <summary>
        /// 请求方法，如 GET、POST
        /// </summary>
        public string Method
        {
            get; set;
        }

        /// <summary>
        /// 不含协议与域名的相对地址
        /// </summary>
        public string RelativeUrl
        {
            get; set;
        }

        /// <summary>
        /// 完整请求地址
        /// </summary>
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

        /// <summary>
        /// 协议名与版本，如 HTTP/1.1
        /// </summary>
        public string Protocal
        {
            get; set;
        }

        /// <summary>
        /// 请求携带的 Cookie 集合
        /// </summary>
        public virtual HttpCookies Cookies
        {
            get; set;
        } = new HttpCookies();


        /// <summary>
        /// 报头键值集合，键不区分大小写
        /// </summary>
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
                var key = _contentTypeKey;
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
                var key = _contentTypeKey;
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


        /// <summary>
        /// 是否为 multipart/form-data 表单请求
        /// </summary>
        public bool IsFormData { get; set; } = false;

        /// <summary>
        /// multipart 表单的分隔串
        /// </summary>
        public string Boundary
        {
            get; set;
        }

        /// <summary>
        /// 报文体字节长度
        /// </summary>
        public int ContentLength
        {
            get; set;
        }

        /// <summary>
        /// 报文体内容
        /// </summary>
        public byte[] Body
        {
            get; set;
        }

        /// <summary>
        /// 按枚举描述读取报头值
        /// </summary>
        /// <param name="header">报头枚举</param>
        /// <returns>报头值，键不存在时抛出 KeyNotFoundException</returns>
        protected string GetHeader(Enum header)
        {
            var fieldName = header.GetDescription();
            if (fieldName == null) return null;
            return Headers[fieldName];
        }

        /// <summary>
        /// 按枚举描述写入报头值
        /// </summary>
        /// <param name="header">报头枚举</param>
        /// <param name="value">报头值</param>
        protected void SetHeader(Enum header, string value)
        {
            var fieldName = header.GetDescription();
            if (fieldName == null) return;
            Headers[fieldName] = value;
        }

        /// <summary>
        /// 按枚举描述移除报头
        /// </summary>
        /// <param name="header">报头枚举</param>
        /// <param name="value">当前实现未使用</param>
        protected void RemoveHeader(Enum header, string value)
        {
            var fieldName = header.GetDescription();
            if (fieldName == null) return;
            var hasKey = Headers.ContainsKey(fieldName);
            if (hasKey) Headers.Remove(fieldName);
        }
    }
}