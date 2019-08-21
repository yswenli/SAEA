/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Base
*文件名： HttpServerUtilityBase
*版本号： v5.0.0.1
*唯一标识：01f783cd-c751-47c5-a5b9-96d3aa840c70
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/16 11:03:29
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/16 11:03:29
*修改人： yswenli
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SAEA.Http.Base
{
    /// <summary>
    /// HttpUtility
    /// .core
    /// </summary>
    public class HttpUtility
    {
        string _root = "";

        List<FileInfo> files = new List<FileInfo>();

        List<DirectoryInfo> dirs = new List<DirectoryInfo>();

        static ConcurrentDictionary<string, string> _cache = new ConcurrentDictionary<string, string>();


        public HttpUtility(string root = "wwwroot")
        {
            _root = root;

            if (string.IsNullOrEmpty(_root))
            {
                _root = "wwwroot";
            }

            _root = _root.Replace("/", "").Replace("\\", "");

            var rootDir = SAEA.Common.PathHelper.GetCurrentPath(_root);

            var dir = new DirectoryInfo(rootDir);

            dirs = PathHelper.GetAllDirectories(dir, out files);
        }


        /// <summary>
        /// MapPath
        /// </summary>
        /// <param name="uriPath"></param>
        /// <returns></returns>
        public string MapPath(string uriPath)
        {
            return _cache.GetOrAdd(uriPath, (k) =>
            {
                var cpath = _root + "/" + uriPath;

                var list = cpath.Split("/", StringSplitOptions.RemoveEmptyEntries).ToList();

                list.Insert(0, SAEA.Common.PathHelper.GetCurrentPath());

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
        /// UrlEncode
        /// </summary>
        /// <param name="s"></param>
        /// <param name="isData"></param>
        /// <returns></returns>
        public static string UrlEncode(string s, bool isData = false)
        {
            if (isData)
            {
                return Uri.EscapeDataString(s);
            }
            return Uri.EscapeUriString(s);

        }
        /// <summary>
        /// UrlDecode
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string UrlDecode(string s)
        {
            return Uri.UnescapeDataString(s);
        }

        /// <summary>
        /// HtmlEncode
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string HtmlEncode(string str)
        {
            return System.Web.HttpUtility.HtmlEncode(str);
        }
        /// <summary>
        /// HtmlDecode
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        public static string HtmlDecode(string html)
        {
            return System.Web.HttpUtility.HtmlDecode(html);
        }
    }
}
