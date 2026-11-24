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
*文件名： HttpMessage
*版本号： v26.4.23.1
*唯一标识：ea096853-9c8d-4c82-97c9-6556bcdb064d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/11/07 14:25:13
*描述：HttpMessage消息类
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 15:59:54
*修改人： yswenli
*版本号： v26.11.24.1
*描述：新增文件分段落盘阈值与临时目录配置，供流式 multipart 解析使用
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Http.Model;

namespace SAEA.Http.Base
{
    /// <summary>
    /// Http 消息实体，承载解析后的请求行、报头与报文体
    /// </summary>
    public class HttpMessage : HttpBase, IDisposable
    {
        // 报头原文字节保存在池化缓冲里，HeaderStr 被读取时才解码成字符串；
        // 缓冲由消息实例持有，复位或释放时归还内存池
        byte[] _headerBuffer;

        int _headerLength;

        string _headerStr;

        // Cookie 报头原文与解析结果分开保存：解析推迟到 Cookies 被读取时，
        // 不读 Cookie 的请求就不必付出集合、Cookie 实体与名字子串的分配
        string _cookieHeader;

        HttpCookies _cookies;

        bool _cookiesParsed;

        /// <summary>
        /// 原始报头文本，首次读取时按需从池化缓冲解码，未读到报头时为 null
        /// </summary>
        public override string HeaderStr
        {
            get
            {
                var value = _headerStr;

                if (value == null && _headerBuffer != null)
                {
                    value = Encoding.ASCII.GetString(_headerBuffer, 0, _headerLength);
                    _headerStr = value;
                }

                return value;
            }
            set
            {
                _headerStr = value;
            }
        }

        /// <summary>
        /// 请求携带的 Cookie 集合，首次读取时才解析 Cookie 报头；集合实例在连接内复用
        /// </summary>
        public override HttpCookies Cookies
        {
            get
            {
                if (!_cookiesParsed)
                {
                    _cookies = HttpCookies.Parse(_cookieHeader, _cookies);

                    _cookiesParsed = true;
                }

                return _cookies;
            }
            set
            {
                _cookies = value;

                // 显式赋值的集合优先于报头原文，避免读出被覆盖前的解析结果
                _cookiesParsed = true;

                _cookieHeader = null;
            }
        }

        /// <summary>
        /// 消息唯一标识
        /// </summary>
        public string ID
        {
            get; set;
        }

        /// <summary>
        /// 报文体在接收缓冲区中的起始偏移
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// 请求体流，只读且可 Seek；无请求体时为 null，请求结束或复位时由本类释放
        /// </summary>
        public Stream Body
        {
            get; set;
        }

        /// <summary>
        /// 文件分段落盘阈值，单位字节；0 表示全部留在内存。由解码器按配置在解析前写入
        /// </summary>
        internal int LargeBodyThreshold
        {
            get; set;
        }

        /// <summary>
        /// 文件分段临时文件目录，null 或空白时用系统临时目录下的 saea-http。由解码器按配置在解析前写入
        /// </summary>
        internal string TempDirectory
        {
            get; set;
        }

        /// <summary>
        /// 接收到的文件信息
        /// </summary>
        public List<FilePart> PostFiles
        {
            get; set;
        } = new List<FilePart>();

        /// <summary>
        /// 报文体按 application/json 解析出的原始文本
        /// </summary>
        public string Json
        {
            get; set;
        }

        /// <summary>
        /// 清空参数集合、报头缓冲、请求体流与文件分段
        /// </summary>
        public void Dispose()
        {
            if (this.Query != null)
                this.Query.Clear();
            if (this.Forms != null)
                this.Forms.Clear();
            if (this.Parmas != null)
                this.Parmas.Clear();

            // 请求体流要么钉住整帧池化缓冲、要么持有临时文件，必须显式释放
            ReleaseBody();

            ReleasePostFiles();

            // 请求结束后不再需要报头原文，及时归还池化缓冲
            ReleaseHeaderSource();
        }

        /// <summary>
        /// 把报头原文复制进池化缓冲，供 <see cref="HeaderStr"/> 按需解码
        /// </summary>
        /// <param name="header">报头原文字节</param>
        internal void SetHeaderSource(ReadOnlySpan<byte> header)
        {
            ReleaseHeaderSource();

            // 新报头会替换旧内容，已解码的旧文本必须一起清掉，避免复用实例读到上一条请求的报头
            _headerStr = null;

            var length = header.Length;

            if (length == 0) return;

            var rented = MemoryPoolManager.Rent(length);

            header.CopyTo(rented);

            _headerBuffer = rented;
            _headerLength = length;
        }

        /// <summary>
        /// 归还报头原文占用的池化缓冲；可重复调用
        /// </summary>
        internal void ReleaseHeaderSource()
        {
            var buffer = _headerBuffer;

            if (buffer == null) return;

            _headerBuffer = null;

            var length = _headerLength;

            _headerLength = 0;

            MemoryPoolManager.Return(buffer, length);
        }

        /// <summary>
        /// 记录 Cookie 报头原文，实际解析推迟到 <see cref="Cookies"/> 首次被读取时
        /// </summary>
        /// <param name="cookieStr">Cookie 报头文本，无 Cookie 时传 null</param>
        internal void SetCookieHeader(string cookieStr)
        {
            _cookieHeader = cookieStr;

            // 新报头会替换旧内容，旧的解析结果等下次读取时才重建
            _cookiesParsed = false;
        }

        /// <summary>
        /// 直接在 Cookie 报头原文里查找指定名字的 Cookie，不建立集合
        /// </summary>
        /// <param name="name">Cookie 名，区分大小写，与集合键的规则一致</param>
        /// <param name="value">Cookie 值，已按集合的解析规则解码；名字不存在时为 null</param>
        /// <returns>存在该名字的 Cookie 返回 true</returns>
        internal bool TryGetCookieValue(string name, out string value)
        {
            // 集合已被显式赋值或读取过时以集合为准，否则与报头原文可能不一致
            if (_cookiesParsed)
            {
                if (_cookies != null && _cookies.TryGetValue(name, out var cookie))
                {
                    value = cookie == null ? null : cookie.Value;

                    return true;
                }

                value = null;

                return false;
            }

            return HttpCookies.TryGetValue(_cookieHeader, name, out value);
        }

        /// <summary>
        /// 复位消息，使同一个实例可以承载本连接的下一条请求；复位后的状态与新建实例一致
        /// </summary>
        internal void ResetForNextRequest()
        {
            ReleaseHeaderSource();

            _headerStr = null;

            ID = null;
            Position = 0;
            Json = null;
            Method = null;
            RelativeUrl = null;
            Url = null;
            Protocal = null;
            IsFormData = false;
            Boundary = null;
            ContentLength = 0;

            // 复用前先释放上一条请求的体与文件，否则整帧缓冲与临时文件会留到下一次请求
            ReleaseBody();

            ReleasePostFiles();

            // 集合沿用原实例只清空内容：比较器与容量都保留，语义等同新建的空集合
            if (Query == null)
                Query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            else
                Query.Clear();

            if (Forms == null)
                Forms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            else
                Forms.Clear();

            if (Parmas == null)
                Parmas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            else
                Parmas.Clear();

            if (Headers == null)
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            else
                Headers.Clear();

            // Cookie 报头原文与解析状态必须一起复位，否则下一次请求会读到上一次的 Cookie
            _cookieHeader = null;

            _cookiesParsed = false;

            if (_cookies == null)
                _cookies = new HttpCookies();
            else
                _cookies.Clear();

            if (PostFiles == null)
                PostFiles = new List<FilePart>();
            else
                PostFiles.Clear();
        }

        /// <summary>
        /// 释放请求体流并置空；可重复调用
        /// </summary>
        void ReleaseBody()
        {
            var body = Body;

            Body = null;

            if (body == null) return;

            try
            {
                body.Dispose();
            }
            catch (Exception ex)
            {
                // 释放失败不能中断连接收口，留日志即可
                LogHelper.Error("HttpMessage.ReleaseBody", ex);
            }
        }

        /// <summary>
        /// 释放全部文件分段的流并清空集合；可重复调用
        /// </summary>
        void ReleasePostFiles()
        {
            var files = PostFiles;

            if (files == null) return;

            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];

                if (file == null) continue;

                try
                {
                    file.Dispose();
                }
                catch (Exception ex)
                {
                    LogHelper.Error("HttpMessage.ReleasePostFiles", ex);
                }
            }

            files.Clear();
        }

    }
}