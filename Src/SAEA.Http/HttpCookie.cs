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
*文件名： HttpCookie
*版本号： v26.4.23.1
*唯一标识：fe911aa8-9f8f-4dea-87be-e85225c9cfad
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/12/13 15:23:48
*描述：HttpCookie接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/18 16:48:11
*修改人： yswenli
*版本号： v26.11.16.1
*描述：Cookie 序列化改为单次预置容量，并抽出分段写出供响应报头字节路径共用，输出文本保持不变
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http.Base;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.Http
{
    /// <summary>
    /// 单个 Cookie 的键值、过期时间、路径与域
    /// </summary>
    public class HttpCookie
    {
        internal static string DefaultDomain { get; set; } = string.Empty;

        /// <summary>
        /// Cookie 名
        /// </summary>
        public string Key
        {
            get; set;
        }

        /// <summary>
        /// Cookie 值
        /// </summary>
        public string Value
        {
            get; set;
        }

        /// <summary>
        /// 过期时间，年份不大于 1 时按会话 Cookie 输出
        /// </summary>
        public DateTime Expires
        {
            get; set;
        }

        /// <summary>
        /// 生效路径
        /// </summary>
        public string Path
        {
            get; set;
        }

        /// <summary>
        /// 生效域，未设置时取默认域
        /// </summary>
        public string Domain
        {
            get; set;
        }


        /// <summary>
        /// 以键值创建 Cookie，路径默认为根路径
        /// </summary>
        /// <param name="key">Cookie 名</param>
        /// <param name="value">Cookie 值</param>
        public HttpCookie(string key, string value)
        {
            this.Key = key;
            this.Value = value;
            this.Path = "/";
        }

        /// <summary>
        /// 以键值加过期时间创建 Cookie，路径默认为根路径
        /// </summary>
        /// <param name="key">Cookie 名</param>
        /// <param name="value">Cookie 值</param>
        /// <param name="expires">过期时间</param>
        public HttpCookie(string key, string value, DateTime expires)
        {
            this.Key = key;
            this.Value = value;
            this.Expires = expires;
            this.Path = "/";
        }

        /// <summary>
        /// 以键值、过期时间、路径与域创建 Cookie
        /// </summary>
        /// <param name="key">Cookie 名</param>
        /// <param name="value">Cookie 值</param>
        /// <param name="expires">过期时间</param>
        /// <param name="path">生效路径</param>
        /// <param name="domain">生效域</param>
        public HttpCookie(string key, string value, DateTime expires, string path, string domain)
        {
            this.Key = key;
            this.Value = value;
            this.Expires = expires;
            this.Path = path;
            this.Domain = domain;
        }


        /// <summary>
        /// 输出 Set-Cookie 报头文本
        /// </summary>
        /// <returns>报头文本，Key 为空时返回空字符串</returns>
        public new string ToString()
        {
            if (string.IsNullOrEmpty(Key))
                return string.Empty;

            var builder = new StringBuilder(GetEstimatedLength());
            var sink = new StringBuilderHeaderSink(builder);
            WriteChunks(ref sink);

            return builder.ToString();
        }

        /// <summary>
        /// 按 Set-Cookie 分段写出到报头写出目标：报头字符串与报头字节两条输出都走这里，保证文本一致
        /// </summary>
        /// <param name="sink">报头分段写出目标</param>
        internal void WriteChunks<TSink>(ref TSink sink) where TSink : struct, IHttpHeaderChunkSink
        {
            sink.Text("Set-Cookie: ");
            sink.Text(Key);
            sink.Text("=");
            sink.Text(Value);
            sink.Text(";");

            sink.Text(" Domain=");
            sink.Text(string.IsNullOrEmpty(this.Domain) ? DefaultDomain : this.Domain);
            sink.Text(";");

            sink.Text(" Expires=");
            sink.Text(Expires.Year > 1 ? Expires.ToGMTString() : "Session");
            sink.Text(";");

            sink.Text(" Path=");
            sink.Text(string.IsNullOrEmpty(Path) ? "/" : Path);
        }

        /// <summary>
        /// 估算本 Cookie 的 Set-Cookie 文本长度，用于一次预置 StringBuilder 容量
        /// </summary>
        /// <returns>估算的字符数，固定段与前缀按 48 计</returns>
        int GetEstimatedLength()
        {
            // 固定段：前缀 "Set-Cookie: "、两个分隔分号、三个属性名前后的空格与 "="；过期时间按最长 29 字符预留
            var length = 48;
            length += Key.Length;
            length += Value == null ? 0 : Value.Length;
            length += string.IsNullOrEmpty(this.Domain) ? DefaultDomain.Length : this.Domain.Length;
            length += string.IsNullOrEmpty(Path) ? 1 : Path.Length;
            return length;
        }
    }

    /// <summary>
    /// Cookie 集合，键为 Cookie 名
    /// </summary>
    public class HttpCookies : Dictionary<string, HttpCookie>
    {
        /// <summary>
        /// 解析 Cookie 报头文本，值做 URL 与 HTML 解码
        /// </summary>
        /// <param name="cookieStr">Cookie 报头文本，分号分隔多个 Cookie</param>
        /// <returns>解析出的 Cookie 集合，入参为空时返回空集合</returns>
        public static HttpCookies Parse(string cookieStr)
        {
            var result = new HttpCookies();

            if (string.IsNullOrEmpty(cookieStr)) return result;

            var rows = cookieStr.Split(";", StringSplitOptions.RemoveEmptyEntries);

            if (rows == null || rows.Count() <= 0) return result;

            foreach (var row in rows)
            {
                var rowArr = row.Split("=");
                if (rowArr.Length == 2)
                {
                    result[rowArr[0].Trim()] = new HttpCookie(rowArr[0].Trim(), HttpUtility.HtmlDecode(HttpUtility.UrlDecode(rowArr[1])));
                }
                else
                {
                    result[rowArr[0].Trim()] = new HttpCookie(rowArr[0].Trim(), "");
                }
            }
            return result;
        }

        /// <summary>
        /// 创建空的 Cookie 集合
        /// </summary>
        public HttpCookies() : base()
        {

        }

        /// <summary>
        /// 输出全部 Set-Cookie 报头文本，每个 Cookie 一行
        /// </summary>
        /// <returns>报头文本，集合为空时返回空字符串</returns>
        public new string ToString()
        {
            if (this.Count > 0)
            {
                // 一次算够容量并直接分段写入，避免每个 Cookie 先成串再被 StringBuilder 复制一次
                var capacity = 0;
                foreach (var item in this.Values)
                {
                    if (item == null || string.IsNullOrEmpty(item.Key)) continue;
                    capacity += item.Key.Length + (item.Value == null ? 0 : item.Value.Length) + 48 + Environment.NewLine.Length;
                }

                if (capacity == 0) return string.Empty;

                StringBuilder sb = new StringBuilder(capacity);
                var sink = new StringBuilderHeaderSink(sb);
                foreach (var item in this.Values)
                {
                    if (item == null || string.IsNullOrEmpty(item.Key)) continue;
                    item.WriteChunks(ref sink);
                    sink.Break();
                }
                return sb.ToString();
            }
            return string.Empty;
        }

        /// <summary>
        /// 把指定 Cookie 的过期时间置为当前时间，使其立即失效
        /// </summary>
        /// <param name="name">Cookie 名，不存在时不做处理</param>
        public void Expire(string name)
        {
            if (this.ContainsKey(name))
            {
                var cookie = this[name];
                if (cookie != null)
                {
                    cookie.Expires = DateTimeHelper.Now;
                }
            }
        }
    }
}