/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Commom.IO
*文件名： Class1
*版本号： v5.0.0.1
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
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SAEA.Common.IO
{
    public sealed class PathHelper
    {
        static string _currentPath = string.Empty;

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

        public static DirectoryInfo GetParent(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                return Directory.GetParent(path);
            }
            return null;
        }

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


        public static string Combine(params string[] dirNames)
        {
            return Path.Combine(dirNames);
        }


        public static string Combine(string dirPath, params string[] dirNames)
        {
            List<string> list = new List<string>();

            list.Add(dirPath);

            list.AddRange(dirNames);

            return Combine(list.ToArray());
        }

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


        public static bool CreateDir(string root, string dirName)
        {
            if (Combine(root, dirName, out string dirPath))
            {
                CreateDir(dirPath);
                return true;
            }
            return false;
        }

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
