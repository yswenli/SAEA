/****************************************************************************
*项目名称：SAEA.RedisSocket.Core.Stream
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core.Stream
*类 名 称：RedisGroupConsumer
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/12 19:48:34
*描述：
*=====================================================================
*修改时间：2021/1/12 19:48:34
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.RedisSocket.Core.Stream
{
    /// <summary>
    /// redisconsumer,
    /// XREADGROUP GROUP $GroupName $ConsumerName BLOCK 2000 COUNT 10 STREAMS mystream
    /// XGROUP CREATE mystream consumer-group-name $ MKSTREAM
    /// </summary>
    public class RedisGroupConsumer : IDisposable
    {
        bool _started = false;

        RedisQueue _redisQueue;

        int _count = 1;
        bool _blocked = false;
        int _timeout = 1000;

        //

        string _groupName = string.Empty;
        string _consumerName = string.Empty;
        string _topicName = string.Empty;
        string _redisId = "0";
        bool _noAck = false;
        bool _asc = true;
        bool _autoCommit = false;

        //
        bool _inited = false;


        /// <summary>
        /// 接收到数据事件
        /// </summary>
        public event Action<RedisGroupConsumer, IEnumerable<StreamEntry>> OnReceive;
        /// <summary>
        /// 异常
        /// </summary>
        public event Action<Exception> OnError;



        /// <summary>
        /// RedisGroupConsumer
        /// </summary>
        /// <param name="redisConnection"></param>
        /// <param name="groupName"></param>
        /// <param name="consumerName"></param>
        /// <param name="topicName"></param>
        /// <param name="count"></param>
        /// <param name="autoCommit"></param>
        /// <param name="redisId"></param>
        /// <param name="noAck"></param>
        /// <param name="blocked"></param>
        /// <param name="timeout"></param>
        /// <param name="asc"></param>
        internal RedisGroupConsumer(RedisConnection redisConnection, string groupName, string consumerName, string topicName, int count = 1, bool autoCommit = false, string redisId = "", bool noAck = false, bool blocked = false, int timeout = 1000, bool asc = true)
        {
            _redisQueue = new RedisQueue(redisConnection);

            _groupName = groupName;
            _consumerName = consumerName;
            _topicName = topicName;
            _redisId = redisId;
            _noAck = noAck;
            _count = count;
            _autoCommit = autoCommit;
            _blocked = blocked;
            _timeout = timeout;
            _asc = asc;
        }

        /// <summary>
        /// Start
        /// </summary>
        public void Start()
        {
            if (!_started)
            {
                _started = true;

                while (_started)
                {
                    try
                    {
                        var list = SubscribeWithGroup().ToList();

                        if (list != null && list.Any())
                        {
                            OnReceive.Invoke(this, list);
                        }
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke(ex);
                    }
                }
            }
        }

        /// <summary>
        /// 创建组
        /// </summary>
        /// <returns></returns>
        public bool CreateGroup()
        {
            return _redisQueue.CreateGroup(_topicName, _groupName, _asc);
        }
        /// <summary>
        /// 移除组
        /// </summary>
        /// <returns></returns>
        public bool RemoveGroup()
        {
            return _redisQueue.RemoveGroup(_topicName, _groupName);
        }

        /// <summary>
        /// SubscribeWithGroup
        /// </summary>
        /// <returns></returns>
        public IEnumerable<StreamEntry> SubscribeWithGroup()
        {
            if (!_inited)
            {
                CreateGroup();
                _inited = true;
            }
            var data = _redisQueue.Subscribe(_groupName, _consumerName, _topicName, string.IsNullOrEmpty(_redisId) ? null : new RedisID(_redisId), _noAck, _count, _blocked, _timeout);

            if (_autoCommit)
            {
                var ids = new List<RedisID>();

                foreach (var item in data)
                {
                    var ilist = item.IdFileds;

                    foreach (var sitem in ilist)
                    {
                        ids.Add(sitem.RedisID);
                    }
                }
                Commit(ids);
            }
            return data;
        }

        /// <summary>
        /// 提交
        /// </summary>
        /// <param name="redisIDs"></param>
        /// <returns></returns>
        public int Commit(IEnumerable<RedisID> redisIDs)
        {
            return _redisQueue.Commit(_topicName, _groupName, redisIDs);
        }


        /// <summary>
        /// Stop
        /// </summary>
        public void Stop()
        {
            _started = false;
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            _redisQueue.Dispose();
        }
    }
}
