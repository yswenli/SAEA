/****************************************************************************
*项目名称：SAEA.FTP.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTP.Core
*类 名 称：FTPStream
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/9/27 17:14:24
*描述：
*=====================================================================
*修改时间：2019/9/27 17:14:24
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
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

                var i = span.IndexOf((byte)10);

                if (i > -1)
                {
                    var j = span.IndexOf((byte)13);

                    if (j > i)
                    {
                        var len = j + 1;

                        var str = Encoding.ASCII.GetString(span.Slice(0, len).ToArray());

                        _cache.RemoveRange(0, len);

                        return str;
                    }
                }
                return null;
            }
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
