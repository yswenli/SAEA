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
*命名空间：SAEA.FTP.Core
*文件名： FTPDataManager
*版本号： v26.4.23.1
*唯一标识：9ea77fa5-df38-45d2-9c73-7c88dd495631
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/08 09:18:57
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/11/08 09:18:57
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
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
