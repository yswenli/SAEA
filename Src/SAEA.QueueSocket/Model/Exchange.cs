/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common.Caching;
using SAEA.Sockets.Interface;

namespace SAEA.QueueSocket.Model
{
    /// <summary>
    /// Exchange类，实现ISyncBase接口
    /// </summary>
    class Exchange : ISyncBase
    {
        // 同步锁对象
        object _syncLocker = new object();

        // 分类批量打包器
        ClassificationBatcher _classificationBatcher;

        /// <summary>
        /// 分类批量打事件
        /// </summary>
        public event OnClassificationBatchedHandler OnBatched;

        /// <summary>
        /// 获取同步锁对象
        /// </summary>
        public object SyncLocker
        {
            get
            {
                return _syncLocker;
            }
        }

        // 发布者数量
        long _pNum = 0;

        // 订阅者数量
        long _cNum = 0;

        // 接收消息数量
        long _inNum = 0;

        // 发送消息数量
        long _outNum = 0;

        // 绑定对象
        private Binding _binding;

        // 消息队列对象
        private MessageQueue _messageQueue;

        /// <summary>
        /// 初始化Exchange类的新实例
        /// </summary>
        public Exchange()
        {
            _binding = new Binding();

            _messageQueue = new MessageQueue();

            _classificationBatcher = ClassificationBatcher.GetInstance(10000, 100);

            _classificationBatcher.OnBatched += _classificationBatcher_OnBatched;
        }

        /// <summary>
        /// 分类批量打事件处理程序
        /// </summary>
        /// <param name="id">分类ID</param>
        /// <param name="data">数据</param>
        private void _classificationBatcher_OnBatched(string id, byte[] data)
        {
            OnBatched?.Invoke(id, data);
        }

        /// <summary>
        /// 接受发布消息
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="pInfo">队列消息</param>
        public void AcceptPublish(string sessionID, QueueMsg pInfo)
        {
            _binding.Set(sessionID, pInfo.Name, pInfo.Topic);

            _messageQueue.Enqueue(pInfo.Topic, pInfo.Data);

            _pNum = _binding.GetPublisherCount();

            Interlocked.Increment(ref _inNum);
        }

        /// <summary>
        /// 获取订阅数据
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="sInfo">队列消息</param>
        /// <param name="qcoder">队列编码器</param>
        public void GetSubscribeData(string sessionID, QueueMsg sInfo, Net.QueueCoder qcoder)
        {
            if (!_binding.Exists(sInfo))
            {
                _binding.Set(sessionID, sInfo.Name, sInfo.Topic, false);

                _cNum = _binding.GetSubscriberCount();

                Task.Factory.StartNew((Func<Task>)(async () =>
                {
                    while (_binding.Exists(sInfo))
                    {
                        var msg = await _messageQueue.DequeueAsync(sInfo.Topic);
                        if (msg != null && msg.Length > 0)
                        {
                            Interlocked.Increment(ref _outNum);
                            _classificationBatcher.Insert(sessionID, qcoder.Data(sInfo.Name, sInfo.Topic, msg));
                        }
                    }
                }), TaskCreationOptions.LongRunning);
            }
        }

        /// <summary>
        /// 取消订阅
        /// </summary>
        /// <param name="sInfo">队列消息</param>
        public void Unsubscribe(QueueMsg sInfo)
        {
            Interlocked.Decrement(ref _cNum);
            _binding.Del(sInfo.Name, sInfo.Topic);
        }

        /// <summary>
        /// 清除会话
        /// </summary>
        /// <param name="sessionID">会话ID</param>
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

        /// <summary>
        /// 获取连接信息
        /// </summary>
        /// <returns>连接信息元组</returns>
        public Tuple<long, long, long, long> GetConnectInfo()
        {
            return new Tuple<long, long, long, long>(_pNum, _cNum, _inNum, _outNum);
        }

        /// <summary>
        /// 获取队列信息
        /// </summary>
        /// <returns>队列信息列表</returns>
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
