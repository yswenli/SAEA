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
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Common.Threading;

namespace SAEA.RedisSocket.Base.Net
{
    /// <summary>
    /// RedisStream
    /// </summary>
    internal class RedisStream : IDisposable
    {
        BlockingCollection<byte[]> _queue = new BlockingCollection<byte[]>();

        List<byte> _bytes = new List<byte>();

        BlockingQueue<string> _stringQueue = new BlockingQueue<string>();

        public bool IsDisposed { get; private set; } = false;

        int _timeout = 6 * 1000;

        /// <summary>
        /// RedisStream
        /// </summary>
        /// <param name="timeout"></param>
        public RedisStream(int timeout = 6 * 1000)
        {
            _timeout = timeout;

            TaskHelper.LongRunning(() =>
            {
                while (!IsDisposed)
                {
                    if (!_queue.TryTake(out byte[] data, timeout))
                    {
                        continue;
                    }

                    if (data == null || data.Length == 0) continue;

                    _bytes.AddRange(data);

                    do
                    {
                        var index = _bytes.IndexOf(13);

                        if (index == -1)
                        {
                            break;
                        }

                        //双回车结束的情况
                        if (_bytes.IndexOf(10, index) == index + 1)
                        {
                            index += 1;
                        }
                        else
                        {
                            break;
                        }

                        var count = index + 1;

                        var str = Encoding.UTF8.GetString(_bytes.Take(count).ToArray());

                        _stringQueue.Enqueue(str);

                        _bytes.RemoveRange(0, count);
                    }
                    while (!IsDisposed);
                }

            });
        }

        /// <summary>
        /// 存入收到的内容
        /// </summary>
        /// <param name="memory"></param>
        public void Write(byte[] memory)
        {
            _queue.Add(memory);
        }

        /// <summary>
        /// 读取出队内容
        /// </summary>
        /// <returns></returns>
        public string ReadLine()
        {
            return _stringQueue.Dequeue(_timeout);
        }

        /// <summary>
        /// 读取指定长度内容
        /// </summary>
        /// <param name="len"></param>
        /// <param name="ctoken"></param>
        /// <returns></returns>
        public string ReadBlock(int len, CancellationToken ctoken)
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                while (!ctoken.IsCancellationRequested && (sb.Length < len && Encoding.UTF8.GetByteCount(sb.ToString()) < len))
                {
                    sb.Append(ReadLine());
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "-Err:ReadBlock Timeout," + ex.Message;
            }
        }

        public void Clear()
        {
            _stringQueue.Clear();
        }

        public void Dispose()
        {
            IsDisposed = true;
            Clear();
        }
    }
}
