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

        ConcurrentQueue<string> _stringQueue = new ConcurrentQueue<string>();

        bool _isdiposed = false;

        public RedisStream()
        {
            Task.Factory.StartNew(() =>
            {
                while (!_isdiposed)
                {
                    if (_queue.TryDequeue(out byte[] data))
                    {
                        if (data == null || !data.Any()) continue;

                        _bytes.AddRange(data);

                        var index = -1;

                        do
                        {
                            index = _bytes.IndexOf(10);

                            if (index < 0)
                            {
                                break;
                            }

                            //双回车结束的情况
                            if (_bytes.IndexOf(10, index) == index + 2)
                            {
                                index += 2;
                            }

                            index += 1;

                            _stringQueue.Enqueue(Encoding.UTF8.GetString(_bytes.Take(index).ToArray()));

                            _bytes.RemoveRange(0, index);
                        }
                        while (!_isdiposed);
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
            if(_stringQueue.TryDequeue(out string result))
            {
                return result;
            }
            return null;
        }

        public string ReadBlock(int len)
        {
            StringBuilder sb = new StringBuilder();

            while (sb.Length < len)
            {
                sb.Append(ReadLine());
            }

            return sb.ToString();
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
