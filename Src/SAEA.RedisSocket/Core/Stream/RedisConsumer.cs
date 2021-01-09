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


        /// <summary>
        /// 连接包装类
        /// </summary>
        internal RedisConnection RedisConnection { get; private set; }

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
