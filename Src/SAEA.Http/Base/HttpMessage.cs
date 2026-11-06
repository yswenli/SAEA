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
*修改时间：2026/11/6 12:52:29
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;

using SAEA.Http.Model;

namespace SAEA.Http.Base
{
    /// <summary>
    /// Http 消息实体，承载解析后的请求行、报头与报文体
    /// </summary>
    public class HttpMessage : HttpBase, IDisposable
    {
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
        /// 清空参数集合与报文体缓冲
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
        }

    }
}