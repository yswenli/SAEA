/****************************************************************************
*项目名称：SAEA.RedisSocket.Core.Stream
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core.Stream
*类 名 称：RedisConsumer
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/8 17:38:25
*描述：
*=====================================================================
*修改时间：2021/1/8 17:38:25
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
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
