/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.QueueSocket.Model
*文件名： QQueue
*版本号： v6.0.0.1
*唯一标识：e3581012-3090-41b4-ae4d-58ee74c08094
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/5 16:38:19
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/5 16:38:19
*修改人： yswenli
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Sockets.Interface;

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SAEA.QueueSocket.Model
{
    public class QueueBase : ISyncBase, IDisposable
    {
        BlockingQueue<string> _queue;

        public string Topic
        {
            get; private set;
        }

        long _length;

        public long Length { get => _length; set => _length = value; }


        object _syncLocker = new object();

        public object SyncLocker
        {
            get
            {
                return _syncLocker;
            }
        }

        public QueueBase(string topic)
        {
            _queue = new BlockingQueue<string>();
            this.Topic = topic;
            _length = 0;
        }

        public void Enqueue(string data)
        {
            _queue.Enqueue(data);
            Interlocked.Increment(ref _length);
        }

        public string Dequeue()
        {
            var result = _queue.Dequeue();

            Interlocked.Decrement(ref _length);

            return result;
        }

        public void Dispose()
        {
            _queue.Clear();
            _queue = null;
        }
    }
}
