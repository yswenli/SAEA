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
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Core.Stream
{
    /// <summary>
    /// redisconsumer,
    /// XREAD BLOCK 5000 COUNT 100 STREAMS mystream $,
    /// XREADGROUP GROUP $GroupName $ConsumerName BLOCK 2000 COUNT 10 STREAMS mystream
    /// XGROUP CREATE mystream consumer-group-name $ MKSTREAM
    /// </summary>
    public class RedisConsumer
    {
        bool _started = false;

        RedisQueue _redisQueue;

        IEnumerable<TopicID> _topicIDs;
        int _count = 1;
        bool _blocked = false;
        int _timeout = 1000;

        //

        string _groupName = string.Empty;
        string _consumerName = string.Empty;
        string _topicName = string.Empty;

        //
        bool _inited = false;
        //

        Func<IEnumerable<RedisField>> _streamDataHandler;


        /// <summary>
        /// 接收到数据
        /// </summary>
        public event Action<IEnumerable<RedisField>> OnReceive;
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

            _streamDataHandler = new Func<IEnumerable<RedisField>>(() => _redisQueue.Subscribe(topicIDs, count, blocked, timeout));
        }

        /// <summary>
        /// redisconsumer
        /// </summary>
        /// <param name="redisConnection"></param>
        /// <param name="groupName"></param>
        /// <param name="consumerName"></param>
        /// <param name="topicName"></param>
        /// <param name="count"></param>
        /// <param name="blocked"></param>
        /// <param name="timeout"></param>
        /// <param name="asc"></param>
        internal RedisConsumer(RedisConnection redisConnection, string groupName, string consumerName, string topicName, int count = 1, bool blocked = false, int timeout = 1000, bool asc = true)
        {
            _redisQueue = new RedisQueue(redisConnection);

            _groupName = groupName;
            _consumerName = consumerName;
            _topicName = topicName;
            _count = count;
            _blocked = blocked;
            _timeout = timeout;

            _streamDataHandler = new Func<IEnumerable<RedisField>>(() =>
            {
                if (!_inited)
                {
                    _redisQueue.CreateGroup(_topicName, _groupName, _consumerName, asc);
                    _inited = true;
                }
                return _redisQueue.Subscribe(_groupName, _consumerName, _topicName, _count, _blocked, _timeout);
            });
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
                        OnReceive.Invoke(_streamDataHandler.Invoke());
                    }
                    catch(Exception ex)
                    {
                        OnError?.Invoke(ex);
                    }
                }
            }
        }

        /// <summary>
        /// Stop
        /// </summary>
        public void Stop()
        {
            _started = false;
        }
    }
}
