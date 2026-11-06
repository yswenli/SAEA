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
*唯一标识：4fa760d6-b442-4c13-b788-221b0715e141
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/08 09:18:57
*描述：FTPDataManager管理类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:28
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
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
    /// <summary>
    /// 服务端数据通道的接收缓冲：文件传输时落盘到临时文件，目录列表时缓存在内存
    /// </summary>
    public class FTPDataManager
    {
        #region dowload file

        string _tempPath = string.Empty;

        string _filePath = string.Empty;

        /// <summary>
        /// 开始接收一个上传文件，创建临时文件并将后续数据写入其中
        /// </summary>
        /// <param name="filePath">接收完成后临时文件移动到的目标文件路径</param>
        public void New(string filePath)
        {
            _tempPath = PathHelper.GetFilePath(PathHelper.GetCurrentPath("Data"), Guid.NewGuid().ToString("N") + ".temp");

            _filePath = filePath;

            this.IsFile = true;

            if (_list != null) _list.Clear();

            _list = new List<byte>();
        }

        /// <summary>
        /// 追加接收一段数据；文件模式写入临时文件，目录列表模式追加到内存，空数据直接忽略
        /// </summary>
        /// <param name="data">本次收到的数据</param>
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

        /// <summary>
        /// 把临时文件移动到目标路径；目标已存在时抛 <see cref="IOException"/>
        /// </summary>
        public void FileComplete()
        {
            File.Move(_tempPath, _filePath);
        }
        #endregion

        #region download data

        List<byte> _list = null;

        /// <summary>
        /// 切换到目录列表模式并清空内存缓存
        /// </summary>
        public void Refresh()
        {
            this.IsFile = false;

            if (_list != null) _list.Clear();

            _list = new List<byte>();
        }

        /// <summary>
        /// 通知数据接收完成，目前无附加处理
        /// </summary>
        public void NoticeComplete()
        {
        }

        /// <summary>
        /// 阻塞读取当前缓存的全部内容，缓存为空时每 500 毫秒轮询一次
        /// </summary>
        /// <returns>按 UTF-8 解码的缓存文本</returns>
        public string ReadAllText()
        {
            while (_list.Count < 1)
            {
                ThreadHelper.Sleep(500);
            }
            return Encoding.UTF8.GetString(_list.ToArray());
        }

        #endregion

        /// <summary>
        /// 是否按文件方式接收；为 false 时数据缓存在内存
        /// </summary>
        public bool IsFile { get; set; } = false;
    }
}
