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
*唯一标识：be3f27d8-9512-4e73-9e0e-470ba0a1e99c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/08/25 12:09:47
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/08/25 12:09:47
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
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

            var rootDir = PathHelper.GetCurrentPath(_root);

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
        /// UrlEncode
        /// </summary>
        /// <param name="s"></param>
        /// <param name="isData"></param>
        /// <returns></returns>
        public static string UrlEncode(string s)
        {
            return System.Web.HttpUtility.UrlEncode(s);

        }
        /// <summary>
        /// UrlDecode
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string UrlDecode(string s)
        {
            return System.Web.HttpUtility.UrlDecode(s);
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