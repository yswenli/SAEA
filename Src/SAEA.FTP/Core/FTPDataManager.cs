﻿/****************************************************************************
*项目名称：SAEA.FTP.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTP.Core
*类 名 称：FTPDataManager
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/7 16:41:09
*描述：
*=====================================================================
*修改时间：2019/11/7 16:41:09
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.IO;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SAEA.FTP.Core
{
    public class FTPDataManager
    {
        #region dowload file

        string _tempPath = string.Empty;

        string _filePath = string.Empty;

        public void New(string filePath)
        {
            _tempPath = PathHelper.GetFilePath(PathHelper.GetCurrentPath("Data"), Guid.NewGuid().ToString("N") + ".temp");

            _filePath = filePath;

            this.IsFile = true;

            if (_list != null) _list.Clear();

            _list = new List<byte>();
        }

        public void Receive(byte[] data)
        {

            if (data != null && data.Any())
            {
                if (IsFile)

                    using (var fs = File.Open(_tempPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
                    {
                        fs.Position = fs.Length;
                        fs.Write(data, 0, data.Length);
                    }
                else
                {
                    _list.AddRange(data);
                }
            }
        }

        /// <summary>
        /// 检查文件长度
        /// </summary>
        /// <param name="size"></param>
        /// <param name="offset"></param>
        public void Checked(long size, ref long offset)
        {
            while (true)
            {
                if (!File.Exists(_tempPath))
                {
                    ThreadHelper.Sleep(50);
                    continue;
                }

                try
                {
                    offset = new FileInfo(_tempPath).Length;
                }
                catch { }
                if (offset < size)
                    ThreadHelper.Sleep(50);
                else
                    break;
            }

            FileComplete();
        }

        public void FileComplete()
        {
            File.Move(_tempPath, _filePath);
        }
        #endregion

        #region download data

        List<byte> _list = null;

        bool _isComplete = false;

        public void Refresh()
        {
            this.IsFile = false;

            if (_list != null) _list.Clear();

            _list = new List<byte>();

            _isComplete = false;
        }

        public void NoticeComplete()
        {
            _isComplete = true;
        }

        public string ReadAllText()
        {
            while (_list.Count < 1)
            {
                ThreadHelper.Sleep(500);
            }
            return Encoding.UTF8.GetString(_list.ToArray());
        }

        #endregion

        public bool IsFile { get; set; } = false;
    }
}
