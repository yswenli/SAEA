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
*命名空间：SAEA.RedisSocket.Base.Net
*文件名： RedisStream
*版本号： v26.4.23.1
*唯一标识：38cb48cb-2ebe-4946-8a74-92827d905fe9
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/07/24 17:49:08
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/07/24 17:49:08
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
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
