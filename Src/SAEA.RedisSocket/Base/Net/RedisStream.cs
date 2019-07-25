/****************************************************************************
*项目名称：SAEA.RedisSocket.Base.Net
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.RedisSocket.Base.Net
*类 名 称：RedisStream
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/7/24 15:08:55
*描述：
*=====================================================================
*修改时间：2019/7/24 15:08:55
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.RedisSocket.Base.Net
{
    internal class RedisStream : IDisposable
    {
        ConcurrentQueue<byte[]> _queue = new ConcurrentQueue<byte[]>();

        List<byte> _bytes = new List<byte>();

        object _locker = new object();

        bool _isdiposed = false;

        public RedisStream()
        {
            Task.Factory.StartNew(() =>
            {
                while (!_isdiposed)
                {
                    if (_queue.TryDequeue(out byte[] data))
                    {
                        lock (_locker)
                        {
                            _bytes.AddRange(data);
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        public void Write(byte[] data)
        {
            _queue.Enqueue(data);
        }

        public string ReadLine()
        {
            lock (_locker)
            {
                var data = _bytes.ToArray();

                var span = data.AsSpan();

                var index = span.IndexOf((byte)13);

                if (index >= 0)
                {
                    index = span.IndexOf((byte)10);

                    if (index > 0)
                    {
                        _bytes.RemoveRange(0, index + 1);

                        return Encoding.UTF8.GetString(span.Slice(0, index + 1).ToArray());
                    }
                    return string.Empty;
                }
                else
                {
                    return string.Empty;
                }
            }

        }

        public string Read(int len)
        {            
            lock (_locker)
            {
                if (_bytes.Count < len + 2) return string.Empty;

                var data = _bytes.Take(len).ToArray();

                _bytes.RemoveRange(0, len + 2);

                return Encoding.UTF8.GetString(data);
            }
        }

        public void Clear()
        {
            _bytes.Clear();
        }

        public void Dispose()
        {
            _isdiposed = true;
            _bytes.Clear();
            _bytes = null;
        }
    }
}
