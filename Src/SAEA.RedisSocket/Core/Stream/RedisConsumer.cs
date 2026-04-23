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
*文件名： RedisConsumer
*版本号： v26.4.23.1
*唯一标识：dc3f292c-2500-4ab1-951f-52a74a47e54f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/01/08 20:00:21
*描述：RedisConsumer接口
*
*=====================================================================
*修改标记
*修改时间：2021/01/08 20:00:21
*修改人： yswenli
*版本号： v26.4.23.1
*描述：RedisConsumer接口
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.RedisSocket.Core.Stream
{
    /// <summary>
    /// redisconsumer,
    /// XREAD BLOCK 5000 COUNT 100 STREAMS mystream $
    /// </summary>
    public class RedisConsumer : IDisposable
    {
        bool _started = false;

        RedisQueue _redisQueue;

        IEnumerable<TopicID> _topicIDs;
        int _count = 1;
        bool _blocked = false;
        int _timeout = 1000;


        /// <summary>
        /// 接收到数据事件
        /// </summary>
        public event Action<RedisConsumer, IEnumerable<StreamEntry>> OnReceive;
        /// <summary>
        /// 异常
        /// </summary>
        public event Action<Exception> OnError;

        /// <summary>
        /// redisconsumer
        /// </summary>
        /// <param name="redisConnection"></param>
        /// <param name="topicIDs"></param>
        /// <param name="count"></param>
        /// <param name="blocked"></param>
        /// <param name="timeout"></param>
        internal RedisConsumer(RedisConnection redisConnection, IEnumerable<TopicID> topicIDs, int count = 1, bool blocked = false, int timeout = 1000)
        {
            _redisQueue = new RedisQueue(redisConnection);

            _topicIDs = topicIDs;
            _count = count;
            _blocked = blocked;
            _timeout = timeout;
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
                        var list = Subscribe().ToList();

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
        /// Subscribe
        /// </summary>
        /// <returns></returns>
        public IEnumerable<StreamEntry> Subscribe()
        {
            return _redisQueue.Subscribe(_topicIDs, _count, _blocked, _timeout);
        }

        /// <summary>
        /// Stop
        /// </summary>
        public void Stop()
        {
            _started = false;
        }

        public void Dispose()
        {
            _redisQueue.Dispose();
        }
    }
}
