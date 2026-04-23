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
*命名空间：SAEA.RedisSocket.Core.Stream
*文件名： RedisGroupConsumer
*版本号： v26.4.23.1
*唯一标识：cd522e9c-f4ff-49eb-8c0f-38a73580b0e0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/01/12 19:59:43
*描述：RedisGroupConsumer接口
*
*=====================================================================
*修改标记
*修改时间：2021/01/12 19:59:43
*修改人： yswenli
*版本号： v26.4.23.1
*描述：RedisGroupConsumer接口
*
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
