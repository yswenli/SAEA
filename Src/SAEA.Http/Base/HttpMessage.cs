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
*修改时间：2026/11/19 23:31:33
*修改人： yswenli
*版本号： v26.11.16.1
*描述：报头文本改为按需从池化缓冲生成，新增 ResetForNextRequest 供同连接复用消息实例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

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
        /// 清空参数集合、报头缓冲与报文体缓冲
        /// </summary>
        public void Dispose()
        {
            if (this.Query != null)
                this.Query.Clear();
            if (this.Forms != null)
                this.Forms.Clear();
            if (this.Parmas != null)
                this.Parmas.Clear();
            if (Body != null)
                Array.Clear(Body, 0, Body.Length);

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
            Body = null;

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

            if (Cookies == null)
                Cookies = new HttpCookies();
            else
                Cookies.Clear();

            if (PostFiles == null)
                PostFiles = new List<FilePart>();
            else
                PostFiles.Clear();
        }

    }
}