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
*修改时间：2026/11/19 23:31:33
*修改人： yswenli
*版本号： v26.11.16.1
*描述：Set-Cookie 按需输出属性，空域与未设置的过期时间不再写成 Domain= 与 Expires=Session；新增可写入复用集合的 Cookie 解析重载
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http.Base;

using System;
using System.Collections.Generic;
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

            // 属性按需输出：空域与未设置的过期时间整段省略。写成 Domain= 与 Expires=Session 不是合法属性值，
            // 客户端不会回传这种 Cookie，服务端就只能为每个请求新建会话。
            var domain = string.IsNullOrEmpty(this.Domain) ? DefaultDomain : this.Domain;

            if (!string.IsNullOrEmpty(domain))
            {
                sink.Text("; Domain=");
                sink.Text(domain);
            }

            if (Expires.Year > 1)
            {
                sink.Text("; Expires=");
                sink.Text(Expires.ToGMTString());
            }

            sink.Text("; Path=");
            sink.Text(string.IsNullOrEmpty(Path) ? "/" : Path);
        }

        /// <summary>
        /// 估算本 Cookie 的 Set-Cookie 文本长度，用于一次预置 StringBuilder 容量
        /// </summary>
        /// <returns>与实际输出文本一致的字符数</returns>
        internal int GetEstimatedLength()
        {
            var length = 13; // 前缀 "Set-Cookie: " 与键值之间的 "="
            length += Key.Length;
            length += Value == null ? 0 : Value.Length;

            var domain = string.IsNullOrEmpty(this.Domain) ? DefaultDomain : this.Domain;

            if (!string.IsNullOrEmpty(domain))
                length += 9 + domain.Length; // "; Domain="

            if (Expires.Year > 1)
                length += 10 + 29; // "; Expires=" 与最长 29 字符的 GMT 时间

            length += 7; // "; Path="
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
            return Parse(cookieStr, null);
        }

        /// <summary>
        /// 解析 Cookie 报头文本并写入可复用的集合
        /// </summary>
        /// <param name="cookieStr">Cookie 报头文本，分号分隔多个 Cookie</param>
        /// <param name="reuse">复用的集合，为 null 时新建</param>
        /// <returns>解析出的 Cookie 集合，入参为空时返回空集合</returns>
        internal static HttpCookies Parse(string cookieStr, HttpCookies reuse)
        {
            var result = reuse ?? new HttpCookies();

            if (reuse != null) result.Clear();

            if (string.IsNullOrEmpty(cookieStr)) return result;

            // 按分号逐段扫描并跳过空段，省掉整段拆分的数组与子串
            var start = 0;

            while (start < cookieStr.Length)
            {
                var end = cookieStr.IndexOf(ConstHelper.SEMICOLON, start);

                if (end < 0) end = cookieStr.Length;

                if (end > start) AddCookie(result, cookieStr, start, end);

                start = end + 1;
            }

            return result;
        }

        /// <summary>
        /// 解析单个 Cookie 段并写入集合
        /// </summary>
        /// <param name="cookies">目标集合</param>
        /// <param name="text">Cookie 报头全文</param>
        /// <param name="start">段起始下标</param>
        /// <param name="end">段结束下标（不含）</param>
        static void AddCookie(HttpCookies cookies, string text, int start, int end)
        {
            var equals = text.IndexOf('=', start, end - start);

            // 原实现只在恰好一个等号时取值，无等号与多等号都按空值处理
            if (equals < 0 || text.IndexOf('=', equals + 1, end - equals - 1) > -1)
            {
                var bareKey = TrimName(text, start, equals < 0 ? end : equals);

                cookies[bareKey] = new HttpCookie(bareKey, string.Empty);

                return;
            }

            var key = TrimName(text, start, equals);

            cookies[key] = new HttpCookie(key, HttpUtility.HtmlDecode(HttpUtility.UrlDecode(text.Substring(equals + 1, end - equals - 1))));
        }

        /// <summary>
        /// 截取 Cookie 名并去掉两侧空白
        /// </summary>
        /// <param name="text">Cookie 报头全文</param>
        /// <param name="start">起始下标</param>
        /// <param name="end">结束下标（不含）</param>
        /// <returns>去掉两侧空白后的 Cookie 名</returns>
        static string TrimName(string text, int start, int end)
        {
            while (start < end && char.IsWhiteSpace(text[start])) start++;

            while (end > start && char.IsWhiteSpace(text[end - 1])) end--;

            return text.Substring(start, end - start);
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
                    capacity += item.GetEstimatedLength() + Environment.NewLine.Length;
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