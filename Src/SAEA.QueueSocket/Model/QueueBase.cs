/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.QueueSocket.Model
*文件名： QQueue
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/

using SAEA.Commom;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SAEA.QueueSocket.Model
{
    public class QueueBase : ISyncBase, IDisposable
    {
        ConcurrentQueue<byte[]> _queue;

        AutoResetEvent _autoResetEvent;

        public string Topic
        {
            get; private set;
        }

        long _length;

        public long Length { get => _length; set => _length = value; }

        public DateTime Created
        {
            get; private set;
        }


        int _minute = 60 * 24 * 7;

        public DateTime Expired
        {
            get; private set;
        }

        object _syncLocker = new object();

        public object SyncLocker
        {
            get
            {
                return _syncLocker;
            }
        }

        public QueueBase(string topic, int minutes = 60 * 24 * 7)
        {
            _queue = new ConcurrentQueue<byte[]>();
            this.Topic = topic;
            _length = 0;
            this.Created = DateTimeHelper.Now;
            _minute = minutes;
            this.Expired = DateTimeHelper.Now.AddMinutes(_minute);
            _autoResetEvent = new AutoResetEvent(false);
        }

        public void Enqueue(byte[] data)
        {
            _queue.Enqueue(data);
            this.Expired = DateTimeHelper.Now.AddMinutes(_minute);
            Interlocked.Increment(ref _length);
            _autoResetEvent.Set();
        }

        public byte[] Dequeue()
        {
            if (_queue.TryDequeue(out byte[] result))
            {
                Interlocked.Decrement(ref _length);
            }
            return result;
        }

        public byte[] BlockDequeue()
        {
            byte[] result = null;
            do
            {
                _autoResetEvent.WaitOne();
                result = Dequeue();
            }
            while (result == null);
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
