/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Commom
*文件名： Class1
*版本号： v4.5.1.2
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v4.5.1.2
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SAEA.Common
{
    public sealed class PathHelper
    {
        static string _currentPath = string.Empty;

        public static string GetCurrentPath()
        {
            //return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (string.IsNullOrEmpty(_currentPath))
            {
                _currentPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(PathHelper)).Location);
            }

            return _currentPath;
        }

        /// <summary>
        /// 获取当前目录
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string GetCurrentPath(Type type)
        {
            return Path.GetDirectoryName(type.Assembly.Location);
        }

        /// <summary>
        /// 获取当前目录下的文件全路径
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string GetFullName(string fileName)
        {
            var path = PathHelper.GetCurrentPath();

            return Path.Combine(path, fileName);
        }

        public static string GetCurrentPath(string children)
        {
            var path = Path.Combine(GetCurrentPath(), children);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }

        public static string GetFilePath(string path, string fileName)
        {
            return Path.Combine(path, fileName);
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
