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
*文件名： FTPStream
*版本号： v26.4.23.1
*唯一标识：131a9b81-1f4e-496b-a028-8f16505d60b6
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/09/27 17:50:27
*描述：FTPStream流类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:28
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.FTP.Core
{
    /// <summary>
    /// FTP 命令字节缓冲，按 CRLF 切分出完整文本行，内部加锁保证线程安全
    /// </summary>
    public class FTPStream
    {

        object _locker = new object();

        List<byte> _cache = new List<byte>();

        /// <summary>
        /// 创建空缓冲
        /// </summary>
        public FTPStream() { }

        /// <summary>
        /// 用初始数据创建缓冲
        /// </summary>
        /// <param name="data">初始数据</param>
        public FTPStream(byte[] data)
        {
            _cache.AddRange(data);
        }

        /// <summary>
        /// 追加数据到缓冲末尾
        /// </summary>
        /// <param name="data">要追加的数据</param>
        public void Write(byte[] data)
        {
            lock (_locker)
            {
                _cache.AddRange(data);
            }
        }

        /// <summary>
        /// 读取并移除缓冲区前 len 字节，缓冲不足时抛出越界异常
        /// </summary>
        /// <param name="len">要读取的字节数</param>
        /// <returns>读取到的字节</returns>
        public byte[] Read(int len)
        {
            lock (_locker)
            {
                var data = _cache.ToArray().AsSpan().Slice(0, len).ToArray();
                if (data.Length == len)
                {
                    _cache.RemoveRange(0, len);
                    return data;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 取出一整行（含结尾 CRLF），使用 UTF-8 解码
        /// </summary>
        /// <returns>完整文本行；尚未收到 CRLF 时返回 null</returns>
        public string ReadLine()
        {
            lock (_locker)
            {
                var span = _cache.ToArray().AsSpan();

                var i = span.IndexOf((byte)13);

                if (i > -1)
                {
                    var j = span.IndexOf((byte)10);

                    if (j > i)
                    {
                        var len = j + 1;

                        var str = Encoding.UTF8.GetString(span.Slice(0, len).ToArray());

                        _cache.RemoveRange(0, len);

                        return str;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// 拼接取出当前缓冲内所有完整行
        /// </summary>
        /// <returns>各完整行拼接后的文本；没有完整行时返回空字符串</returns>
        public string ReadText()
        {
            StringBuilder sb = new StringBuilder();

            var str = string.Empty;

            do
            {
                str = ReadLine();

                if (!string.IsNullOrEmpty(str))
                {
                    sb.Append(str);
                }
                else
                {
                    break;
                }
            }
            while (true);

            return sb.ToString();
        }


        /// <summary>
        /// 清空缓冲区
        /// </summary>
        public void Clear()
        {
            lock (_locker)
            {
                _cache.Clear();
            }
        }
        //https://blog.csdn.net/qq_21882325/article/details/73302959
    }
}
