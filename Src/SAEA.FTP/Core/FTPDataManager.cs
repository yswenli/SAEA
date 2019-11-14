/****************************************************************************
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
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
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

        public void Checked(long size)
        {
            long current = 0;

            while (true)
            {
                if (!File.Exists(_tempPath))
                {
                    ThreadHelper.Sleep(500);
                    continue;
                }

                try
                {
                    using (var fs = File.Open(_tempPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        current = fs.Length;
                    }
                }
                catch { }
                if (current < size)
                    ThreadHelper.Sleep(50);
                else
                    break;
            }

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
            while (!_isComplete)
            {
                ThreadHelper.Sleep(500);
            }
            return Encoding.UTF8.GetString(_list.ToArray());
        }

        #endregion

        public bool IsFile { get; set; } = false;
    }
}
