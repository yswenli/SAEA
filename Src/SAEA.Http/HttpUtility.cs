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
*修改时间：2026/11/19 23:31:33
*修改人： yswenli
*版本号： v26.11.16.1
*描述：MapPath 规范解析 . 与 .. 段并校验结果位于站点根目录内，越界路径返回 null；UrlDecode 在无转义与加号时直接返回原串
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.IO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security;

namespace SAEA.Http
{
    /// <summary>
    /// Http 服务器静态资源工具，负责把 URL 映射到物理路径并提供编码方法
    /// </summary>
    public class HttpUtility
    {
        string _root = "";

        string _rootPath = "";

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

            // 归一化后的站点根目录，后续用它做越界判定
            _rootPath = Path.GetFullPath(rootDir);

            var dir = new DirectoryInfo(rootDir);

            dirs = PathHelper.GetAllDirectories(dir, out files);
        }


        /// <summary>
        /// 把 URL 路径映射为物理路径，结果按 URL 缓存
        /// </summary>
        /// <param name="uriPath">URL 路径</param>
        /// <returns>命中已扫描文件时返回其完整路径，未命中时返回拼接出的物理路径；路径越出站点根目录或含非法字符时返回 null</returns>
        public string MapPath(string uriPath)
        {
            if (uriPath == null)
            {
                return null;
            }

            if (_cache.TryGetValue(uriPath, out var cached))
            {
                return cached;
            }

            var physicalPath = ResolvePhysicalPath(uriPath);

            // 越界或非法路径不写入缓存，避免把 null 放进 ConcurrentDictionary
            if (physicalPath == null)
            {
                return null;
            }

            return _cache.GetOrAdd(uriPath, physicalPath);
        }

        /// <summary>
        /// 解析 URL 路径并校验结果仍位于站点根目录之下
        /// </summary>
        /// <param name="uriPath">URL 路径</param>
        /// <returns>站点内的物理路径，越界或非法时返回 null</returns>
        string ResolvePhysicalPath(string uriPath)
        {
            string relativePath;

            if (!TryNormalizeRelativePath(uriPath, out relativePath))
            {
                return null;
            }

            string physicalPath;

            try
            {
                physicalPath = relativePath.Length == 0
                    ? _rootPath
                    : Path.GetFullPath(Path.Combine(_rootPath, relativePath));
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (PathTooLongException)
            {
                return null;
            }
            catch (SecurityException)
            {
                return null;
            }

            // 比较时带上目录分隔符，避免同前缀的兄弟目录被误判为站点内
            var rootWithSeparator = _rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            var isInRoot = string.Equals(physicalPath, _rootPath, StringComparison.OrdinalIgnoreCase)
                || physicalPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);

            if (!isInRoot)
            {
                return null;
            }

            foreach (var item in files)
            {
                if (string.Equals(item.FullName, physicalPath, StringComparison.OrdinalIgnoreCase))
                {
                    return item.FullName;
                }
            }

            return physicalPath;
        }

        /// <summary>
        /// 把 URL 路径拆成规范的相对段：处理 . 与 ..，并把反斜杠视作分隔符
        /// </summary>
        /// <param name="uriPath">URL 路径</param>
        /// <param name="relativePath">规范后的相对路径，无有效段时为空字符串</param>
        /// <returns>可安全拼接时为 true；绝对路径、UNC 路径、越界或含非法字符时为 false</returns>
        static bool TryNormalizeRelativePath(string uriPath, out string relativePath)
        {
            relativePath = null;

            if (uriPath == null || uriPath.IndexOf('\0') >= 0)
            {
                return false;
            }

            // Windows 下反斜杠同样是目录分隔符，先统一再拆分
            var path = uriPath.Replace('\\', '/');

            // 拒绝 UNC 路径与盘符路径，它们会绕过相对路径约束
            if (path.StartsWith("//", StringComparison.Ordinal))
            {
                return false;
            }

            if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':')
            {
                return false;
            }

            if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                return false;
            }

            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            var stack = new List<string>(segments.Length);

            foreach (var segment in segments)
            {
                if (segment == ".")
                {
                    continue;
                }

                if (segment == "..")
                {
                    // 没有可回退的段说明已越过站点根目录
                    if (stack.Count == 0)
                    {
                        return false;
                    }

                    stack.RemoveAt(stack.Count - 1);
                    continue;
                }

                if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    return false;
                }

                stack.Add(segment);
            }

            relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), stack);

            return true;
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
            // 既没有 % 转义也没有 + 时解码结果与原串一致，直接返回省掉一次复制
            if (string.IsNullOrEmpty(s) || (s.IndexOf('%') < 0 && s.IndexOf('+') < 0)) return s;

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