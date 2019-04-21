/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.QueueSocket.Model
*文件名： QueueCollection
*版本号： v4.5.1.2
*唯一标识：89a65c12-c4b3-486b-a933-ad41c3db6621
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/6 10:31:11
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/6 10:31:11
*修改人： yswenli
*版本号： v4.5.1.2
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAEA.QueueSocket.Model
{
    public class MessageQueue : ISyncBase, IDisposable
    {
        bool _isDisposed = false;

        ConcurrentDictionary<string, QueueBase> _list;

        object _syncLocker = new object();

        public object SyncLocker
        {
            get
            {
                return _syncLocker;
            }
        }

        public MessageQueue()
        {
            _list = new ConcurrentDictionary<string, QueueBase>();

            ThreadHelper.Run(() =>
            {
                while (!_isDisposed)
                {
                    var list = _list.Values.Where(b => b.Expired <= DateTimeHelper.Now);
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            if (item.Length == 0)
                            {
                                _list.TryRemove(item.Topic, out QueueBase q);
                            }
                        }
                    }
                    ThreadHelper.Sleep(10000);
                }
            }, true, System.Threading.ThreadPriority.Highest);
        }


        public void Enqueue(string topic, string data)
        {
            var queue = _list.Values.FirstOrDefault(b => b.Topic.Equals(topic));
            lock (_syncLocker)
            {
                if (queue == null)
                {
                    queue = new QueueBase(topic);
                    _list.TryAdd(topic, queue);
                }                
            }
            queue.Enqueue(data);
        }


        public string Dequeue(string topic)
        {
            var queue = _list.Values.FirstOrDefault(b => b.Topic.Equals(topic));
            if (queue != null)
            {
                return queue.Dequeue();
            }
            return null;
        }

        /// <summary>
        /// 批量读取数据
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="maxSize"></param>
        /// <param name="maxTime"></param>
        /// <returns></returns>
        public List<string> DequeueForList(string topic, int maxSize = 500, int maxTime = 500)
        {
            List<string> result = new List<string>();
            bool running = true;
            var m = 0;
            var task = Task.Factory.StartNew(() =>
            {
                while (running)
                {
                    var data = Dequeue(topic);
                    if (data != null)
                    {
                        result.Add(data);
                        m++;
                        if (m == maxSize)
                        {
                            running = false;
                        }
                    }
                    else
                    {
                        ThreadHelper.Sleep(1);
                    }
                }
            });
            Task.WaitAll(new Task[] { task }, maxTime);
            running = false;
            return result;
        }

        public List<QueueBase> ToList()
        {
            lock (_syncLocker)
            {
                return _list.Values.ToList();
            }
        }

        public long GetCount(string topic)
        {
            var queue = _list.Values.FirstOrDefault(b => b.Topic == topic);
            if (queue != null)
                return queue.Length;
            return 0;
        }

        public void Dispose()
        {
            _isDisposed = true;
            _list.Clear();
                _list = null;
        }
    }
}
