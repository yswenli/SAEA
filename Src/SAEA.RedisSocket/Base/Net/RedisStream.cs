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
using SAEA.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.RedisSocket.Base.Net
{
    /// <summary>
    /// RedisStream
    /// </summary>
    internal class RedisStream : IDisposable
    {
        ConcurrentQueue<byte[]> _queue = new ConcurrentQueue<byte[]>();

        List<byte> _bytes = new List<byte>();

        ConcurrentQueue<string> _stringQueue = new ConcurrentQueue<string>();

        AutoResetEvent _autoResetEvent;

        bool _isdiposed = false;

        /// <summary>
        /// RedisStream
        /// </summary>
        public RedisStream()
        {
            _autoResetEvent = new AutoResetEvent(true);

            Task.Factory.StartNew(() =>
            {
                while (!_isdiposed)
                {
                    if (!_queue.TryDequeue(out byte[] data))
                    {
                        if (data == null || !data.Any()) continue;

                        _bytes.AddRange(data);

                        do
                        {
                            var index = _bytes.IndexOf(10);

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
                        _autoResetEvent.WaitOne();
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// 存入收到的内容
        /// </summary>
        /// <param name="data"></param>
        public void Write(byte[] data)
        {
            _queue.Enqueue(data);
            _autoResetEvent.Set();
        }

        /// <summary>
        /// 读取出队内容
        /// </summary>
        /// <returns></returns>
        public string ReadLine()
        {
            _stringQueue.TryDequeue(out string result);

            return result;
        }

        /// <summary>
        /// 读取指定长度内容
        /// </summary>
        /// <param name="len"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public string ReadBlock(int len, int timeOut = 10)
        {
            return TaskHelper.Run((token) =>
            {
                StringBuilder sb = new StringBuilder();

                while (!token.IsCancellationRequested && sb.Length < len)
                {
                    sb.Append(ReadLine());
                }
                return sb.ToString();

            }, timeOut * 1000).Result;
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
