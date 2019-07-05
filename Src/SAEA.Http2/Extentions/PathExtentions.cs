/****************************************************************************
*项目名称：SAEA.Http2.Extentions
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Extentions
*类 名 称：PathExtentions
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/7/1 14:39:52
*描述：
*=====================================================================
*修改时间：2019/7/1 14:39:52
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SAEA.Http2.Extentions
{
    public static class PathExtentions
    {
        static ConcurrentDictionary<string, string> _cache = new ConcurrentDictionary<string, string>();

        static string _root = string.Empty;

        static List<FileInfo> files = new List<FileInfo>();

        static List<DirectoryInfo> dirs = new List<DirectoryInfo>();

        public static void SetRoot(string path)
        {
            _root = path;

            if (string.IsNullOrEmpty(path))
            {
                _root = "wwwroot";
            }

            _root = _root.Replace("/", "").Replace("\\", "");

            var rootDir = SAEA.Common.PathHelper.GetCurrentPath(_root);

            var dir = new DirectoryInfo(rootDir);

            dirs = GetAllDirectories(dir, out files);
        }

        public static string MapPath(this string uriPath)
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
        /// 获取目录下的目录及文件
        /// </summary>
        /// <param name="root"></param>
        /// <param name="files"></param>
        /// <returns></returns>
        public static List<DirectoryInfo> GetAllDirectories(DirectoryInfo root, out List<FileInfo> files)
        {
            List<DirectoryInfo> directories = new List<DirectoryInfo>();

            files = new List<FileInfo>();

            if (root != null)
            {
                directories.Add(root);
            }

            var fs = root.GetFiles();

            if (fs != null && fs.Any())
            {
                files.AddRange(fs);
            }

            var dirs = root.GetDirectories();

            if (dirs != null && dirs.Any())
            {
                foreach (var dir in dirs)
                {
                    List<FileInfo> sfs;

                    directories.AddRange(GetAllDirectories(dir, out sfs));

                    if (sfs != null && sfs.Any())
                    {
                        files.AddRange(sfs);
                    }
                }
            }

            return directories;
        }
    }
}
