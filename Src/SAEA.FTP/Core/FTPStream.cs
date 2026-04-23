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
*修改时间：2019/09/27 17:50:27
*修改人： yswenli
*版本号： v26.4.23.1
*描述：FTPStream流类
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.FTP.Core
{
    public class FTPStream
    {

        object _locker = new object();

        List<byte> _cache = new List<byte>();

        public FTPStream() { }

        public FTPStream(byte[] data)
        {
            _cache.AddRange(data);
        }

        public void Write(byte[] data)
        {
            lock (_locker)
            {
                _cache.AddRange(data);
            }
        }

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
