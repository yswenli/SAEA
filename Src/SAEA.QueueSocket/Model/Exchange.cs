/****************************************************************************
*Copyright (c) 2018-2022yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.QueueSocket.Model
*文件名： Exchange
*版本号： v7.0.0.1
*唯一标识：6a576aad-edcc-446d-b7e5-561a622549bf
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/5 16:36:44
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/5 16:36:44
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common.Caching;
using SAEA.Common.Threading;
using SAEA.QueueSocket.Net;
using SAEA.Sockets.Interface;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SAEA.QueueSocket.Model
{
    class Exchange : ISyncBase
    {
        object _syncLocker = new object();

        ClassificationBatcher _classificationBatcher;

        /// <summary>
        /// 分类批量打事件
        /// </summary>
        public event OnClassificationBatchedHandler OnBatched;

        public object SyncLocker
        {
            get
            {
                return _syncLocker;
            }
        }

        long _pNum = 0;

        long _cNum = 0;

        long _inNum = 0;

        long _outNum = 0;

        private Binding _binding;

        private MessageQueue _messageQueue;

        public Exchange()
        {
            _binding = new Binding();

            _messageQueue = new MessageQueue();

            _classificationBatcher = ClassificationBatcher.GetInstance(10000, 50);

            _classificationBatcher.OnBatched += _classificationBatcher_OnBatched;
        }

        private void _classificationBatcher_OnBatched(string id, byte[] data)
        {
            OnBatched?.Invoke(id, data);
        }

        public void AcceptPublish(string sessionID, QueueResult pInfo)
        {
            _binding.Set(sessionID, pInfo.Name, pInfo.Topic);

            _messageQueue.Enqueue(pInfo.Topic, pInfo.Data);

            _pNum = _binding.GetPublisherCount();

            Interlocked.Increment(ref _inNum);
        }

        public void GetSubscribeData(string sessionID, QueueResult sInfo, QUnpacker qcoder)
        {
            if (!_binding.Exists(sInfo))
            {
                _binding.Set(sessionID, sInfo.Name, sInfo.Topic, false);

                _cNum = _binding.GetSubscriberCount();

                ThreadPool.QueueUserWorkItem(new WaitCallback((o) =>
                {
                    while (_binding.Exists(sInfo))
                    {
                        var msg = _messageQueue.Dequeue(sInfo.Topic);
                        if (!string.IsNullOrEmpty(msg))
                        {
                            Interlocked.Increment(ref _outNum);
                            _classificationBatcher.Insert(sessionID, qcoder.QueueCoder.Data(sInfo.Name, sInfo.Topic, msg));
                        }
                        else
                        {
                            Thread.Yield();
                        }
                    }
                }));
            }
        }

        public void Unsubscribe(QueueResult sInfo)
        {
            Interlocked.Decrement(ref _cNum);
            _binding.Del(sInfo.Name, sInfo.Topic);
        }

        public void Clear(string sessionID)
        {
            lock (_syncLocker)
            {
                var data = _binding.GetBingInfo(sessionID);

                if (data != null)
                {
                    if (data.Flag)
                    {
                        Interlocked.Decrement(ref _pNum);
                    }
                    else
                    {
                        Interlocked.Decrement(ref _cNum);
                    }
                    _binding.Remove(sessionID);
                }
            }
        }

        public Tuple<long, long, long, long> GetConnectInfo()
        {
            return new Tuple<long, long, long, long>(_pNum, _cNum, _inNum, _outNum);
        }


        public List<Tuple<string, long>> GetQueueInfo()
        {
            List<Tuple<string, long>> result = new List<Tuple<string, long>>();
            var dic = _messageQueue.ToList();
            if (!dic.IsEmpty)
            {
                foreach (var item in dic)
                {
                    var count = _messageQueue.GetCount(item.Key);
                    var t = new Tuple<string, long>(item.Key, count);
                    result.Add(t);
                }
            }
            return result;
        }

    }
}
