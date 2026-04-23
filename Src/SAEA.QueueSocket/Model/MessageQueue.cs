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
*命名空间：SAEA.QueueSocket.Model
*文件名： MessageQueue
*版本号： v26.4.23.1
*唯一标识：2c5fa797-7a8e-4cb1-8582-acb70a3ec700
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
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

        private int _maxPendingMsgCount = 10000000;

        /// <summary>
        /// 消息队列
        /// </summary>
        /// <param name="maxPendingMsgCount">消息队列最大堆积数量，默认10000000</param>
        public MessageQueue(int maxPendingMsgCount = 10000000)
        {
            _dic = new ConcurrentDictionary<string, FastQueue<byte[]>>();
            _maxPendingMsgCount = maxPendingMsgCount;
        }


        public ValueTask<bool> Enqueue(string topic, byte[] data)
        {
            if (!_dic.TryGetValue(topic, out FastQueue<byte[]> queue))
            {
                queue = new FastQueue<byte[]>(_maxPendingMsgCount);
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