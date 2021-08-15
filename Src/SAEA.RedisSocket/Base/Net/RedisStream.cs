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
using SAEA.Common.Caching;
using SAEA.Common.Threading;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace SAEA.RedisSocket.Base.Net
{
    /// <summary>
    /// RedisStream
    /// </summary>
    internal class RedisStream : IDisposable
    {
        BlockingQueue<byte[]> _queue = new BlockingQueue<byte[]>();

        List<byte> _bytes = new List<byte>();

        ConcurrentQueue<string> _stringQueue = new ConcurrentQueue<string>();

        public bool IsDisposed { get; private set; } = false;

        /// <summary>
        /// RedisStream
        /// </summary>
        public RedisStream()
        {
            TaskHelper.LongRunning(() =>
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                while (!IsDisposed)
                {
                    var data = _queue.Dequeue();

                    stopwatch.Restart();

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
            _queue.Enqueue(memory);
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
            _queue.Clear();
            _stringQueue.Clear();
            _bytes.Clear();
        }

        public void Dispose()
        {
            IsDisposed = true;
            Clear();
        }
    }
}
