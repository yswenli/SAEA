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
*文件名： HttpUtility
*版本号： v26.4.23.1
*唯一标识：39635345-96ff-40c0-8539-797268ac6f7c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/08/25 12:09:47
*描述：HttpUtility接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:29
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.IO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SAEA.Http
{
    /// <summary>
    /// Http 服务器静态资源工具，负责把 URL 映射到物理路径并提供编码方法
    /// </summary>
    public class HttpUtility
    {
        string _root = "";

        List<FileInfo> files = new List<FileInfo>();

        List<DirectoryInfo> dirs = new List<DirectoryInfo>();

        static ConcurrentDictionary<string, string> _cache = new ConcurrentDictionary<string, string>();


        /// <summary>
        /// 创建静态资源工具并扫描站点的目录与文件
        /// </summary>
        /// <param name="root">网站根目录名，空值回退为 wwwroot，路径分隔符会被去掉</param>
        public HttpUtility(string root = "wwwroot")
        {
            _root = root;

            if (string.IsNullOrEmpty(_root))
            {
                _root = "wwwroot";
            }

            _root = _root.Replace("/", "").Replace("\\", "");

            var rootDir = PathHelper.GetCurrentPath(_root);

            var dir = new DirectoryInfo(rootDir);

            dirs = PathHelper.GetAllDirectories(dir, out files);
        }


        /// <summary>
        /// 把 URL 路径映射为物理路径，结果按 URL 缓存
        /// </summary>
        /// <param name="uriPath">URL 路径</param>
        /// <returns>命中已扫描文件时返回其完整路径，否则返回拼接出的物理路径</returns>
        public string MapPath(string uriPath)
        {
            return _cache.GetOrAdd(uriPath, (k) =>
            {
                var cpath = _root + "/" + uriPath;

                var list = cpath.Split("/", StringSplitOptions.RemoveEmptyEntries).ToList();

                list.Insert(0, PathHelper.GetCurrentPath());

                var physicalPath = Path.Combine(list.ToArray());

                foreach (var item in files)
                {
                    if (item.FullName.ToLowerInvariant() == physicalPath.ToLowerInvariant())
                    {
                        return item.FullName;
                    }
                }
                return physicalPath;
            });
        }
        /// <summary>
        /// 对字符串做 URL 编码
        /// </summary>
        /// <param name="s">待编码文本</param>
        /// <returns>编码后的文本</returns>
        public static string UrlEncode(string s)
        {
            return System.Web.HttpUtility.UrlEncode(s);

        }
        /// <summary>
        /// 对字符串做 URL 解码
        /// </summary>
        /// <param name="s">待解码文本</param>
        /// <returns>解码后的文本</returns>
        public static string UrlDecode(string s)
        {
            return System.Web.HttpUtility.UrlDecode(s);
        }

        /// <summary>
        /// 对字符串做 HTML 编码
        /// </summary>
        /// <param name="str">待编码文本</param>
        /// <returns>编码后的文本</returns>
        public static string HtmlEncode(string str)
        {
            return System.Web.HttpUtility.HtmlEncode(str);
        }
        /// <summary>
        /// 对字符串做 HTML 解码
        /// </summary>
        /// <param name="html">待解码文本</param>
        /// <returns>解码后的文本</returns>
        public static string HtmlDecode(string html)
        {
            return System.Web.HttpUtility.HtmlDecode(html);
        }
    }
}