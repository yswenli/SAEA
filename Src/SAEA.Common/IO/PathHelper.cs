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
*命名空间：SAEA.Common.IO
*文件名： PathHelper
*版本号： v26.4.23.1
*唯一标识：db382fce-9a23-4cd6-a591-7ffb644ea8e4
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/16 17:50:24
*描述：PathHelper帮助类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:45
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SAEA.Common.IO
{
    /// <summary>
    /// 目录与路径操作类
    /// </summary>
    public sealed class PathHelper
    {
        static string _currentPath = string.Empty;

        /// <summary>
        /// 获取当前目录，取基目录的上一级并缓存
        /// </summary>
        /// <returns>当前目录路径</returns>
        public static string GetCurrentPath()
        {
            if (string.IsNullOrEmpty(_currentPath))
            {
                //_currentPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(PathHelper)).Location);

                //if (_currentPath.IndexOf(".nuget\\packages", StringComparison.OrdinalIgnoreCase) > -1)
                //{
                //    _currentPath = Directory.GetCurrentDirectory();
                //}
                _currentPath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
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

        /// <summary>
        /// 获取当前目录下的子目录，不存在时创建
        /// </summary>
        /// <param name="children">子目录名</param>
        /// <returns>子目录完整路径</returns>
        public static string GetCurrentPath(string children)
        {
            var path = Path.Combine(GetCurrentPath(), children);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        /// 拼接目录与文件名得到完整路径
        /// </summary>
        /// <param name="path">目录路径</param>
        /// <param name="fileName">文件名</param>
        /// <returns>完整路径，不校验是否存在</returns>
        public static string GetFilePath(string path, string fileName)
        {
            return Path.Combine(path, fileName);
        }

        /// <summary>
        /// 获取路径中的文件名
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件名，不含目录部分</returns>
        public static string GetFileName(string filePath)
        {
            return Path.GetFileName(filePath);
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
                    if (dir.Name == "$RECYCLE.BIN" || dir.Name == "System Volume Information") continue;

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

        /// <summary>
        /// 获取目录下的目录及文件
        /// </summary>
        /// <param name="root"></param>
        /// <param name="files"></param>
        /// <returns></returns>
        public static List<DirectoryInfo> GetAllDirectories(string root, out List<FileInfo> files)
        {
            files = null;

            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
            {
                var rd = new DirectoryInfo(root);

                return GetAllDirectories(rd, out files);
            }

            return null;
        }


        /// <summary>
        /// 获取目录下的目录及文件
        /// </summary>
        /// <param name="root"></param>
        /// <param name="files"></param>
        /// <returns></returns>
        public static List<DirectoryInfo> GetDirectories(DirectoryInfo root, out List<FileInfo> files)
        {
            List<DirectoryInfo> directories = new List<DirectoryInfo>();

            files = new List<FileInfo>();

            if (root != null)
            {
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
                        if (dir.Name == "$RECYCLE.BIN" || dir.Name == "System Volume Information") continue;

                        directories.Add(dir);
                    }
                }
            }

            return directories;
        }

        /// <summary>
        /// 获取目录下的目录及文件
        /// </summary>
        /// <param name="root"></param>
        /// <param name="files"></param>
        /// <returns></returns>
        public static List<DirectoryInfo> GetDirectories(string root, out List<FileInfo> files)
        {
            files = null;

            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
            {
                var rd = new DirectoryInfo(root);

                return GetDirectories(rd, out files);
            }

            return null;
        }

        /// <summary>
        /// 获取目录的上级目录
        /// </summary>
        /// <param name="path">目录路径</param>
        /// <returns>上级目录信息，路径为空、目录不存在或已是根目录时返回 null</returns>
        public static DirectoryInfo GetParent(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                return Directory.GetParent(path);
            }
            return null;
        }

        /// <summary>
        /// 判断目录是否存在
        /// </summary>
        /// <param name="dirPath">目录路径</param>
        /// <returns>存在返回 true</returns>
        public static bool Exists(string dirPath)
        {
            return Directory.Exists(dirPath);
        }

        /// <summary>
        /// 合并后判断是否存在并返回合并结果
        /// </summary>
        /// <param name="dirPath"></param>
        /// <param name="dirName"></param>
        /// <param name="newDirPath"></param>
        /// <returns></returns>
        public static bool Combine(string dirPath, string dirName, out string newDirPath)
        {
            newDirPath = string.Empty;

            var dirNames = dirName.Split(new string[] { "/", "\\" }, StringSplitOptions.RemoveEmptyEntries);
            var path = Combine(dirPath, dirNames);
            if (Exists(path))
            {
                newDirPath = path;
                return true;
            }
            else
            {
                if (Exists(dirPath))
                {
                    newDirPath = dirPath;
                }
                return false;
            }
        }


        /// <summary>
        /// 合并多级目录名
        /// </summary>
        /// <param name="dirNames">按顺序拼接的目录名</param>
        /// <returns>合并后的路径</returns>
        public static string Combine(params string[] dirNames)
        {
            return Path.Combine(dirNames);
        }


        /// <summary>
        /// 以根目录加多级子目录名合并路径
        /// </summary>
        /// <param name="dirPath">根目录</param>
        /// <param name="dirNames">子目录名</param>
        /// <returns>合并后的路径</returns>
        public static string Combine(string dirPath, params string[] dirNames)
        {
            List<string> list = new List<string>();

            list.Add(dirPath);

            list.AddRange(dirNames);

            return Combine(list.ToArray());
        }

        /// <summary>
        /// 判断 targetPath 是否与 sourcePath 构成父子层级，路径相同返回 false
        /// </summary>
        /// <param name="sourcePath">源路径</param>
        /// <param name="targetPath">待比较的目标路径</param>
        /// <returns>sourcePath 为根目录、其父目录等于 targetPath 或长度小于 targetPath 时返回 true</returns>
        public static bool IsParent(string sourcePath, string targetPath)
        {
            if (sourcePath == targetPath) return false;

            var parent = new DirectoryInfo(sourcePath).Parent;

            if (parent == null) return true;

            var result = parent.ToString() == targetPath;

            if (result)
            {
                return true;
            }
            else
            {
                if (sourcePath.Length < targetPath.Length) return true;
            }

            return false;
        }


        /// <summary>
        /// 在根目录下创建子目录，子目录已存在时不创建
        /// </summary>
        /// <param name="root">根目录</param>
        /// <param name="dirName">子目录名，可含 / 或 \ 分隔的多级</param>
        /// <returns>目录已存在或创建成功返回 true，根目录下无此目录返回 false</returns>
        public static bool CreateDir(string root, string dirName)
        {
            if (Combine(root, dirName, out string dirPath))
            {
                CreateDir(dirPath);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 创建目录，异常被记录到日志不向外抛出
        /// </summary>
        /// <param name="dirPath">目录路径</param>
        /// <returns>创建成功返回 true，已存在或创建失败返回 false</returns>
        public static bool CreateDir(string dirPath)
        {
            try
            {
                if (Directory.Exists(dirPath))
                {
                    return false;
                }
                Directory.CreateDirectory(dirPath);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Error("PathHelper.CreateDir", ex, dirPath);
            }
            return false;
        }

        /// <summary>
        /// 删除目录，仅删除空目录，异常被记录到日志不向外抛出
        /// </summary>
        /// <param name="dirPath">目录路径</param>
        /// <returns>删除成功返回 true，失败返回 false</returns>
        public static bool Remove(string dirPath)
        {
            try
            {
                Directory.Delete(dirPath);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Error("PathHelper.Remove", ex, dirPath);
            }
            return false;
        }

    }
}