/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.QueueSocket.Model
*文件名： QueueCollection
*版本号： v7.0.0.1
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
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common.Caching;
using SAEA.Sockets.Interface;

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SAEA.QueueSocket.Model
{
    public class MessageQueue : ISyncBase, IDisposable
    {
        readonly ConcurrentDictionary<string, FastQueue<byte[]>> _dic;

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
            _dic = new ConcurrentDictionary<string, FastQueue<byte[]>>();
        }


        public ValueTask<bool> Enqueue(string topic, byte[] data)
        {
            if (!_dic.TryGetValue(topic, out FastQueue<byte[]> queue))
            {
                queue = new FastQueue<byte[]>();
                _dic.TryAdd(topic, queue);
            }
            return queue.EnqueueAsync(data);
        }


        public async ValueTask<byte[]> DequeueAsync(string topic)
        {
            if (_dic.TryGetValue(topic, out FastQueue<byte[]> queue))
            {
                if (queue != null)
                {
                    return await queue.DequeueAsync();
                }
            }
            return null;
        }



        public ConcurrentDictionary<string, FastQueue<byte[]>> ToList()
        {
            return _dic;
        }

        public long GetCount(string topic)
        {
            if (_dic.TryGetValue(topic, out FastQueue<byte[]> queue))
            {
                if (queue != null)
                {
                    return queue.Count;
                }
            }
            return 0;
        }

        public void Dispose()
        {
            _dic.Clear();
        }
    }
}
